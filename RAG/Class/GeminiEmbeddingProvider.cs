using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using System.Net;
using System.Text.Json.Serialization;

namespace RAG.Class
{
    public class GeminiEmbeddingProvider : IEmbeddingProvider
    {
        private const int ErrorBodyLogLimit = 500;

        private readonly HttpClient _httpClient;
        private readonly GeminiEmbeddingModelConfig _config;
        private readonly ILogger<GeminiEmbeddingProvider> _logger;

        public GeminiEmbeddingProvider(HttpClient httpClient,
                                       IOptions<GeminiEmbeddingModelConfig> cfg,
                                       ILogger<GeminiEmbeddingProvider> logger)
        {
            _config = cfg.Value;
            _httpClient = httpClient;
            _logger = logger;
            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_config.Url);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add(GeminiApiDefaults.ApiKeyHeader, _config.ApiKey);
        }

        public string ModelId => _config.Model;

        public async Task<int> GetDimsAsync()
        {
            return await Task.FromResult(_config.OutputDimensions);
        }

        /// <summary>
        /// Giữ nguyên hành vi khoan dung sẵn có của đường truy hồi: lỗi API trả về mảng rỗng
        /// chứ không ném exception, để một lần hỏng không làm gãy request của người dùng.
        /// </summary>
        public async Task<float[]> GetEmbeddingsAsync(string input, CancellationToken cancellationToken = default)
        {
            var result = await PostSingleAsync(input, cancellationToken);
            return result.Vector ?? Array.Empty<float>();
        }

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

                response = await _httpClient.PostAsJsonAsync(_config.BatchUrl, payload, cancellationToken);
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
                var result = await PostSingleAsync(inputs[i], cancellationToken);

                // Dừng ngay khi bị giới hạn tần suất: cố đi tiếp chỉ làm cạn thêm hạn mức
                // và đẩy thời điểm phục hồi ra xa hơn.
                if (result.RateLimited)
                    throw new EmbeddingRateLimitedException(
                        $"Bị giới hạn tần suất sau khi nhúng {i}/{inputs.Count} câu.");

                results[i] = result.Vector ?? Array.Empty<float>();
            }

            return results;
        }

        private async Task<(bool RateLimited, float[]? Vector)> PostSingleAsync(string input,
                                                                                CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync(_config.Url, BuildRequest(input), cancellationToken);

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    return (true, null);

                if (!response.IsSuccessStatusCode)
                    return (false, null);

                var result = await response.Content
                    .ReadFromJsonAsync<GoogleEmbeddingResponse>(cancellationToken: cancellationToken);

                return (false, result?.Embedding?.Values);
            }
        }

        private GoogleEmbeddingRequest BuildRequest(string input) => new()
        {
            Model = _config.Model,
            Content = new GoogleContent { Parts = new[] { new GooglePart { Text = input } } },
            OutputDimensionality = _config.OutputDimensions
        };

        private static string Truncate(string value) =>
            value.Length <= ErrorBodyLogLimit ? value : value[..ErrorBodyLogLimit] + "...";
    }
}

// --- Hệ thống DTOs để Map dữ liệu JSON theo chuẩn của Google ---
public record GoogleEmbeddingRequest
{
    [JsonPropertyName("model")] public string Model { get; init; } = "models/text-embedding-004";
    [JsonPropertyName("content")] public GoogleContent Content { get; init; } = new();
    [JsonPropertyName("output_dimensionality")] public int OutputDimensionality { get; init; } = 1024;
}

public record GoogleContent
{
    [JsonPropertyName("parts")] public GooglePart[] Parts { get; init; } = Array.Empty<GooglePart>();
}

public record GooglePart
{
    [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;
}

public record GoogleEmbeddingResponse
{
    [JsonPropertyName("embedding")] public GoogleVectorData? Embedding { get; init; }
}

public record GoogleVectorData
{
    [JsonPropertyName("values")] public float[] Values { get; init; } = Array.Empty<float>();
}

public record GoogleBatchEmbeddingRequest
{
    [JsonPropertyName("requests")] public GoogleEmbeddingRequest[] Requests { get; init; } = Array.Empty<GoogleEmbeddingRequest>();
}

public record GoogleBatchEmbeddingResponse
{
    [JsonPropertyName("embeddings")] public GoogleVectorData[]? Embeddings { get; init; }
}
