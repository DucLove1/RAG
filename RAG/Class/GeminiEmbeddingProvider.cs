using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;
using System.Net;

namespace RAG.Class
{
    public class GeminiEmbeddingProvider : IEmbeddingProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GeminiEmbeddingModelConfig _config;
        private readonly ILogger<GeminiEmbeddingProvider> _logger;

        public GeminiEmbeddingProvider(IHttpClientFactory httpClientFactory,
                                       IOptions<GeminiEmbeddingModelConfig> cfg,
                                       ILogger<GeminiEmbeddingProvider> logger)
        {
            _config = cfg.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Lấy client đã được cấu hình sẵn ở composition root.
        /// <para>
        /// Bản trước nhận thẳng một <c>HttpClient</c> rồi tự sửa header và <c>BaseAddress</c> ngay trong
        /// constructor — khác hẳn cách <c>GeminiLLMProvider</c> làm, và đặt việc cấu hình hạ tầng
        /// vào trong lớp nghiệp vụ. Giờ hai provider Gemini dùng chung một khuôn.
        /// </para>
        /// </summary>
        private HttpClient CreateClient() => _httpClientFactory.CreateClient(HttpClientNames.GeminiEmbedding);

        public string ModelId => _config.Model;

        public int Dimensions => _config.OutputDimensions;

        /// <summary>
        /// Nhúng một câu.
        /// <para>
        /// NÉM khi nhà cung cấp lỗi, chứ không trả mảng rỗng như bản trước. Mảng rỗng là kết quả
        /// "trông như hợp lệ" nhưng sai: nó đi thẳng vào truy hồi Qdrant, cho ra ngữ cảnh rác, rồi
        /// LLM dựng câu trả lời trên đống rác đó mà không có triệu chứng nào lộ ra ngoài.
        /// </para>
        /// </summary>
        /// <exception cref="EmbeddingRateLimitedException">Bị giới hạn tần suất (429).</exception>
        /// <exception cref="EmbeddingUnavailableException">Lỗi khác, hoặc phản hồi không đọc được.</exception>
        public Task<float[]> GetEmbeddingsAsync(string input, CancellationToken cancellationToken = default) =>
            PostSingleAsync(input, cancellationToken);

        /// <summary>
        /// Nhúng theo lô qua endpoint batchEmbedContents.
        /// <para>
        /// LƯU Ý VỀ QUOTA: batch tiết kiệm thời gian và số round-trip, nhưng Google vẫn tính mỗi câu
        /// trong lô là một request đối với hạn mức embed. Vì vậy lô phải nhỏ hơn hạn mức mỗi phút và
        /// giữa các lô phải có khoảng nghỉ.
        /// </para>
        /// Khi chưa cấu hình BatchUrl, hoặc endpoint không hỗ trợ, sẽ lùi về nhúng từng câu.
        /// Riêng lỗi 429 thì KHÔNG lùi mà ném <see cref="EmbeddingRateLimitedException"/> để caller thử lại sau.
        /// </summary>
        public async Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(IReadOnlyList<string> inputs,
                                                                          CancellationToken cancellationToken = default)
        {
            if (inputs.Count == 0)
                return Array.Empty<float[]>();

            if (string.IsNullOrWhiteSpace(_config.BatchUrl))
            {
                _logger.LogDebug("Chưa cấu hình BatchUrl, nhúng từng câu một cho {Count} câu.", inputs.Count);
                return await EmbedOneByOneAsync(inputs, cancellationToken);
            }

            var batchSize = _config.BatchSize > 0 ? _config.BatchSize : inputs.Count;
            var delay = TimeSpan.FromSeconds(Math.Max(0, _config.BatchDelaySeconds));
            var results = new float[inputs.Count][];

            for (var offset = 0; offset < inputs.Count; offset += batchSize)
            {
                // Nghỉ giữa các lô để không vượt hạn mức request mỗi phút.
                if (offset > 0 && delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Nghỉ {Seconds}s trước lô tiếp theo để tránh vượt hạn mức.", delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }

                var chunk = inputs.Skip(offset).Take(batchSize).ToList();
                var vectors = await TryEmbedChunkAsync(chunk, cancellationToken);

                // Một lô hỏng vì lý do không phải rate limit thì lùi cả danh sách chứ không trộn
                // hai đường đi: trộn dễ tạo ra kết quả lệch chỉ số mà không có triệu chứng nào.
                if (vectors is null)
                    return await EmbedOneByOneAsync(inputs, cancellationToken);

                for (var i = 0; i < chunk.Count; i++)
                    results[offset + i] = vectors[i];
            }

            return results;
        }

        /// <summary>
        /// Trả về null khi lô thất bại vì lý do có thể lùi được.
        /// Ném <see cref="EmbeddingRateLimitedException"/> khi bị giới hạn tần suất — trường hợp này
        /// tuyệt đối không được lùi về nhúng từng câu.
        /// </summary>
        private async Task<IReadOnlyList<float[]>?> TryEmbedChunkAsync(IReadOnlyList<string> chunk,
                                                                       CancellationToken cancellationToken)
        {
            HttpResponseMessage response;

            try
            {
                var payload = new GoogleBatchEmbeddingRequest
                {
                    Requests = chunk.Select(BuildRequest).ToArray()
                };

                response = await CreateClient().PostAsJsonAsync(_config.BatchUrl, payload, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi mạng khi gọi batchEmbedContents, sẽ lùi về nhúng từng câu.");
                return null;
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new EmbeddingRateLimitedException(
                        $"batchEmbedContents bị giới hạn tần suất cho lô {chunk.Count} câu: {Truncate(body)}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Gọi batchEmbedContents thất bại ({Status}): {Body}",
                        (int)response.StatusCode, Truncate(body));
                    return null;
                }

                GoogleBatchEmbeddingResponse? result;
                try
                {
                    result = await response.Content
                        .ReadFromJsonAsync<GoogleBatchEmbeddingResponse>(cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không đọc được phản hồi batchEmbedContents, sẽ lùi về nhúng từng câu.");
                    return null;
                }

                var embeddings = result?.Embeddings;

                // Lệch số lượng nghĩa là không thể ghép an toàn theo chỉ số. Thà chậm còn hơn gán nhầm
                // vector của câu này cho câu khác — lỗi đó hoàn toàn không có triệu chứng.
                if (embeddings is null || embeddings.Length != chunk.Count)
                {
                    _logger.LogWarning("batchEmbedContents trả về {Actual} vector cho {Expected} câu, bỏ lô này.",
                        embeddings?.Length ?? 0, chunk.Count);
                    return null;
                }

                return embeddings.Select(e => e.Values ?? Array.Empty<float>()).ToList();
            }
        }

        private async Task<IReadOnlyList<float[]>> EmbedOneByOneAsync(IReadOnlyList<string> inputs,
                                                                      CancellationToken cancellationToken)
        {
            var results = new float[inputs.Count][];

            // Tuần tự chứ không song song: bắn đồng thời hàng trăm request rất dễ dính rate limit.
            for (var i = 0; i < inputs.Count; i++)
            {
                try
                {
                    results[i] = await PostSingleAsync(inputs[i], cancellationToken);
                }
                catch (EmbeddingRateLimitedException ex)
                {
                    // Dừng ngay khi bị giới hạn tần suất: cố đi tiếp chỉ làm cạn thêm hạn mức
                    // và đẩy thời điểm phục hồi ra xa hơn. Bọc lại để caller biết đã nhúng tới đâu.
                    throw new EmbeddingRateLimitedException(
                        $"Bị giới hạn tần suất sau khi nhúng {i}/{inputs.Count} câu. {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Gọi embedContent cho đúng một câu. Ném cho MỌI trường hợp không lấy được vector dùng được.
        /// </summary>
        private async Task<float[]> PostSingleAsync(string input, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;

            try
            {
                response = await CreateClient().PostAsJsonAsync(_config.Url, BuildRequest(input), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new EmbeddingUnavailableException("Lỗi mạng khi gọi embedContent.", ex);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new EmbeddingRateLimitedException($"embedContent bị giới hạn tần suất: {Truncate(body)}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new EmbeddingUnavailableException(
                        $"embedContent thất bại ({(int)response.StatusCode}): {Truncate(body)}");
                }

                GoogleEmbeddingResponse? result;
                try
                {
                    result = await response.Content
                        .ReadFromJsonAsync<GoogleEmbeddingResponse>(cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new EmbeddingUnavailableException("Không đọc được phản hồi embedContent.", ex);
                }

                var vector = result?.Embedding?.Values;

                // Phản hồi 200 nhưng không có vector vẫn là hỏng. Để lọt xuống dưới thì vector rỗng
                // lại đi vào Qdrant đúng như bug cũ, chỉ khác là qua một đường khác.
                if (vector is null || vector.Length == 0)
                    throw new EmbeddingUnavailableException("embedContent trả về phản hồi không có vector.");

                return vector;
            }
        }

        private GoogleEmbeddingRequest BuildRequest(string input) => new()
        {
            Model = _config.Model,
            Content = new GoogleContent { Parts = new[] { new GooglePart { Text = input } } },
            OutputDimensionality = _config.OutputDimensions
        };

        private string Truncate(string value) =>
            value.Length <= _config.ErrorBodyLogLimit
                ? value
                : value[.._config.ErrorBodyLogLimit] + "...";
    }
}
