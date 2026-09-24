using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace RAG.Class
{
    /// <summary>
    /// <see cref="ILLMProvider"/> và <see cref="ILLMStreamProvider"/> dùng Gemini Interactions API.
    /// Đăng ký kèm khóa <see cref="LlmProviderKey.Gemini"/> để sống chung với GroqCloudProvider.
    /// Luân chuyển API key khi bị 429 (rate limit).
    /// </summary>
    public class GeminiLLMProvider : ILLMProvider, ILLMStreamProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GeminiLlmConfig _config;
        private readonly IApiKeyRotator _rotator;
        private readonly ILogger<GeminiLLMProvider> _logger;

        public GeminiLLMProvider(IHttpClientFactory httpClientFactory,
                                IOptions<GeminiLlmConfig> options,
                                [FromKeyedServices(ApiKeyPoolKey.GeminiLlm)] IApiKeyRotator rotator,
                                ILogger<GeminiLLMProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = options.Value;
            _rotator = rotator;
            _logger = logger;
        }

        public int MaxOutputTokens => _config.MaxOutputTokens;

        public async Task<string> AskAsync(string system, string user, LlmRequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var unavailableRetriesLeft = _config.ServiceUnavailableRetries;

            while (true)
            {
                var key = _rotator.GetCurrentKey();

                // null nghĩa là 429. Trước đây tín hiệu này đi qua message của một
                // HttpRequestException tự dựng, và mệnh đề when bắt nó lại đòi
                // `ex.InnerException is HttpRequestException` — điều KHÔNG BAO GIỜ đúng với một
                // exception dựng bằng constructor một tham số. Hậu quả hoàn toàn im lặng: key
                // Gemini không bao giờ được xoay, và một 429 nổi lên tận RagExceptionHandler rồi
                // rơi vào nhánh mặc định, tức là người chơi nhận 500 thay vì 429. Giờ nó là một
                // nhánh điều khiển bình thường, không còn chuỗi nào để gõ sai.
                var (answer, unavailable) = await TryAskAsync(
                    system, user, options, key, allowUnavailableRetry: unavailableRetriesLeft > 0, cancellationToken);

                if (answer is not null)
                    return answer;

                // 503: máy chủ quá tải, không phải lỗi của key — chờ một chút rồi thử lại CÙNG key,
                // KHÔNG đánh dấu key bị giới hạn. Hết lượt thử thì TryAskAsync tự ném như lỗi HTTP khác.
                if (unavailable)
                {
                    unavailableRetriesLeft--;
                    _logger.LogWarning("Gemini trả 503, thử lại sau {Delay}ms (còn {Left} lượt).",
                        _config.ServiceUnavailableRetryDelayMs, unavailableRetriesLeft);
                    await Task.Delay(_config.ServiceUnavailableRetryDelayMs, cancellationToken);
                    continue;
                }

                // Hết key thì GetCurrentKey() ở vòng sau ném AllApiKeysRateLimitedException.
                _rotator.ReportRateLimited(key);
            }
        }

        /// <returns>
        /// Câu trả lời; hoặc <c>Answer = null</c> khi gặp 429 (caller xoay sang key khác) hay khi gặp
        /// 503 và còn được thử lại (<c>Unavailable = true</c>).
        /// </returns>
        private async Task<(string? Answer, bool Unavailable)> TryAskAsync(string system,
                                                                            string user,
                                                                            LlmRequestOptions? options,
                                                                            string apiKey,
                                                                            bool allowUnavailableRetry,
                                                                            CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.GeminiLlm);

            using var httpRequest = BuildRequest(_config.InteractionsPath, system, user, options, apiKey, stream: false);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return (null, false);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable && allowUnavailableRetry)
                return (null, true);

            await EnsureSuccessAsync(response, cancellationToken);

            var payload = await response.Content
                .ReadFromJsonAsync<GeminiInteractionResponse>(cancellationToken: cancellationToken);

            // Trim CẢ chuỗi một lần ở đây là đúng; đường streaming bên dưới thì tuyệt đối không
            // được trim từng mảnh.
            return (ExtractText(payload).Trim(), false);
        }

        /// <summary>
        /// Bản streaming, đọc SSE từ <c>interactions?alt=sse</c>.
        /// <para>
        /// Cùng cấu trúc hai giai đoạn với <c>GroqCloudProvider.AskStreamAsync</c> và cùng lý do:
        /// C# cấm <c>yield return</c> trong <c>try</c> có <c>catch</c>, nên việc mở kết nối (giai
        /// đoạn DUY NHẤT còn xoay key được) phải nằm trong một method riêng, tách khỏi vòng bơm
        /// token.
        /// </para>
        /// </summary>
        public async IAsyncEnumerable<string> AskStreamAsync(
            string system,
            string user,
            LlmRequestOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(_config.StreamTimeoutSeconds));

            // GIAI ĐOẠN 1 — mở luồng. 429 của Gemini nằm ở status line, tức là trước byte body đầu
            // tiên, nên vẫn xoay key được ở đây và chỉ ở đây.
            HttpResponseMessage? response = null;

            while (response is null)
            {
                var key = _rotator.GetCurrentKey();

                response = await TryOpenStreamAsync(system, user, options, key, deadline.Token);

                if (response is null)
                    _rotator.ReportRateLimited(key);
            }

            // GIAI ĐOẠN 2 — đọc từng dòng. Không có catch nào nên yield return hợp lệ.
            using (response)
            await using (var body = await response.Content.ReadAsStreamAsync(deadline.Token))
            using (var reader = new StreamReader(body, Encoding.UTF8))
            {
                while (await reader.ReadLineAsync(deadline.Token) is { } line)
                {
                    // Dòng trống là ranh giới giữa hai khung; dòng bắt đầu bằng ':' là chú thích
                    // giữ nhịp. Mọi dòng khác không phải "data:" (event:, id:, retry:) đều không
                    // được dùng ở đây.
                    if (line.Length == 0 || line.StartsWith(SseProtocol.CommentPrefix, StringComparison.Ordinal))
                        continue;

                    if (!line.StartsWith(SseProtocol.DataPrefix, StringComparison.Ordinal))
                        continue;

                    var json = line[SseProtocol.DataPrefix.Length..].TrimStart();

                    if (json == SseProtocol.DoneSentinel)
                        break;

                    var streamEvent = JsonSerializer.Deserialize<GeminiInteractionStreamEvent>(json);

                    if (streamEvent?.EventType == GeminiApiDefaults.ErrorEventType)
                        throw new HttpRequestException($"Gemini báo lỗi giữa luồng: {json}");

                    // Chỉ lấy mảnh văn bản; bỏ qua thought, interaction.created/completed, step.start/stop...
                    if (streamEvent?.EventType != GeminiApiDefaults.StepDeltaEventType
                        || streamEvent.Delta?.Type != GeminiApiDefaults.TextContentType)
                        continue;

                    // KHÔNG Trim() ở đây. Đường không streaming trim CẢ chuỗi một lần; trim từng
                    // mảnh sẽ ăn mất khoảng trắng giữa hai token và câu trả lời dính chữ vào nhau.
                    var text = streamEvent.Delta.Text;

                    if (!string.IsNullOrEmpty(text))
                        yield return text;
                }
            }
        }

        /// <returns>Response đã mở, hoặc <c>null</c> khi gặp 429 và caller nên xoay sang key khác.</returns>
        private async Task<HttpResponseMessage?> TryOpenStreamAsync(string system,
                                                                    string user,
                                                                    LlmRequestOptions? options,
                                                                    string apiKey,
                                                                    CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.GeminiLlmStream);

            using var httpRequest = BuildRequest(_config.StreamInteractionsPath, system, user, options, apiKey, stream: true);

            // ResponseHeadersRead là CỜ SỐNG CÒN của cả tính năng. Mặc định (ResponseContentRead),
            // SendAsync không trả về cho tới khi đã đệm xong TOÀN BỘ body — biến một luồng thành
            // một cục và xoá sạch lợi ích, mà không có một triệu chứng lỗi nào: câu trả lời vẫn
            // đúng, chỉ là tất cả đến nơi cùng một lúc ở cuối.
            var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                response.Dispose();
                return null;
            }

            try
            {
                await EnsureSuccessAsync(response, cancellationToken);
            }
            catch
            {
                response.Dispose();
                throw;
            }

            return response;
        }

        /// <summary>
        /// Thay cho <c>EnsureSuccessStatusCode()</c>: Google ghi lý do thật (field sai, giá trị không hỗ
        /// trợ...) trong body, còn exception mặc định vứt body đi và chỉ còn "400 (Bad Request)".
        /// </summary>
        private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Gemini trả {(int)response.StatusCode} ({response.ReasonPhrase}): {body}",
                inner: null,
                response.StatusCode);
        }

        /// <summary>
        /// Dựng request và gắn API key theo TỪNG request chứ không bake vào HttpClient — hai đường
        /// dùng chung một pool key xoay vòng, nên key không thể là thuộc tính của client.
        /// </summary>
        private HttpRequestMessage BuildRequest(string path, string system, string user, LlmRequestOptions? options, string apiKey, bool stream)
        {
            var payload = new GeminiInteractionRequest
            {
                Model = _config.ResolveModel(options?.Model),
                SystemInstruction = string.IsNullOrWhiteSpace(system) ? null : system,
                Input = user,
                Stream = stream,
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = _config.Temperature,
                    MaxOutputTokens = _config.MaxOutputTokens,
                    ThinkingLevel = ResolveThinkingLevel(options)
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(payload)
            };

            httpRequest.Headers.Remove(GeminiApiDefaults.ApiKeyHeader);
            httpRequest.Headers.Add(GeminiApiDefaults.ApiKeyHeader, apiKey);

            return httpRequest;
        }

        /// <summary>
        /// Không có options (đường trả lời chính) thì dùng mức của section GEMINILLM. Có options thì mức của
        /// consumer là tuyệt đối: để trống nghĩa là KHÔNG gửi, chứ không rơi về mức chung — mỗi model
        /// nhận một tập mức khác nhau, rơi về mức chung dễ gửi một giá trị model đó không hỗ trợ (400).
        /// </summary>
        private GeminiThinkingLevel? ResolveThinkingLevel(LlmRequestOptions? options) =>
            options is null
                ? _config.ThinkingLevel
                : options.ThinkingLevel switch
                {
                    null => null,
                    LlmThinkingLevel.Minimal => GeminiThinkingLevel.Minimal,
                    LlmThinkingLevel.Low => GeminiThinkingLevel.Low,
                    LlmThinkingLevel.Medium => GeminiThinkingLevel.Medium,
                    LlmThinkingLevel.High => GeminiThinkingLevel.High,
                    _ => throw new ArgumentOutOfRangeException(nameof(options), options.ThinkingLevel, null)
                };

        /// <summary>Chỉ ghép văn bản của các step <c>model_output</c>; step thought/user_input bị bỏ qua.</summary>
        private static string ExtractText(GeminiInteractionResponse? payload) =>
            payload is null
                ? string.Empty
                : string.Concat(payload.Steps
                    .Where(step => step.Type == GeminiApiDefaults.ModelOutputStepType)
                    .SelectMany(step => step.Content)
                    .Where(content => content.Type == GeminiApiDefaults.TextContentType)
                    .Select(content => content.Text));
    }
}
