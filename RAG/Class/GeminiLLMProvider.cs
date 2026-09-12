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
    /// <see cref="ILLMProvider"/> và <see cref="ILLMStreamProvider"/> dùng Gemini generateContent.
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

        public async Task<string> AskAsync(string system, string user, string? model = null, CancellationToken cancellationToken = default)
        {
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
                var answer = await TryAskAsync(system, user, model, key, cancellationToken);

                if (answer is not null)
                    return answer;

                // Hết key thì GetCurrentKey() ở vòng sau ném AllApiKeysRateLimitedException.
                _rotator.ReportRateLimited(key);
            }
        }

        /// <returns>Câu trả lời, hoặc <c>null</c> khi gặp 429 và caller nên xoay sang key khác.</returns>
        private async Task<string?> TryAskAsync(string system, string user, string? model, string apiKey, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.GeminiLlm);

            using var httpRequest = BuildRequest(_config.BuildGenerateContentPath(model), system, user, apiKey);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return null;

            response.EnsureSuccessStatusCode();

            var payload = await response.Content
                .ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken: cancellationToken);

            // Trim CẢ chuỗi một lần ở đây là đúng; đường streaming bên dưới thì tuyệt đối không
            // được trim từng mảnh.
            return ExtractText(payload).Trim();
        }

        /// <summary>
        /// Bản streaming, đọc SSE từ <c>:streamGenerateContent?alt=sse</c>.
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
            string? model = null,
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

                response = await TryOpenStreamAsync(system, user, model, key, deadline.Token);

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

                    var json = line[SseProtocol.DataPrefix.Length..];

                    if (json == SseProtocol.DoneSentinel)
                        break;

                    // KHÔNG Trim() ở đây. Đường không streaming trim CẢ chuỗi một lần; trim từng
                    // mảnh sẽ ăn mất khoảng trắng giữa hai token và câu trả lời dính chữ vào nhau.
                    var text = ExtractText(JsonSerializer.Deserialize<GeminiGenerateContentResponse>(json));

                    if (text.Length > 0)
                        yield return text;
                }
            }
        }

        /// <returns>Response đã mở, hoặc <c>null</c> khi gặp 429 và caller nên xoay sang key khác.</returns>
        private async Task<HttpResponseMessage?> TryOpenStreamAsync(string system,
                                                                    string user,
                                                                    string? model,
                                                                    string apiKey,
                                                                    CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.GeminiLlmStream);

            using var httpRequest = BuildRequest(_config.BuildStreamGenerateContentPath(model), system, user, apiKey);

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

            response.EnsureSuccessStatusCode();
            return response;
        }

        /// <summary>
        /// Dựng request và gắn API key theo TỪNG request chứ không bake vào HttpClient — hai đường
        /// dùng chung một pool key xoay vòng, nên key không thể là thuộc tính của client.
        /// </summary>
        private HttpRequestMessage BuildRequest(string path, string system, string user, string apiKey)
        {
            var payload = new GeminiGenerateContentRequest
            {
                SystemInstruction = string.IsNullOrWhiteSpace(system)
                    ? null
                    : new GeminiContent { Parts = new[] { new GeminiPart { Text = system } } },
                Contents = new[]
                {
                    new GeminiContent
                    {
                        Role = GeminiApiDefaults.UserRole,
                        Parts = new[] { new GeminiPart { Text = user } }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = _config.Temperature,
                    MaxOutputTokens = _config.MaxOutputTokens,
                    ThinkingConfig = BuildThinkingConfig()
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
        /// Mỗi khung SSE của Gemini mang đúng một <see cref="GeminiGenerateContentResponse"/>, y hệt
        /// hình dạng của đường không streaming — nên cùng một hàm rút text dùng được cho cả hai.
        /// </summary>
        private static string ExtractText(GeminiGenerateContentResponse? payload)
        {
            var parts = payload?.Candidates.FirstOrDefault()?.Content?.Parts;

            return parts is null || parts.Length == 0
                ? string.Empty
                : string.Concat(parts.Select(part => part.Text));
        }

        /// <summary>Không cấu hình mức suy nghĩ nào thì bỏ hẳn thinkingConfig để model chạy theo mặc định.</summary>
        private GeminiThinkingConfig? BuildThinkingConfig() =>
            _config.ThinkingLevel is null && _config.ThinkingBudget is null
                ? null
                : new GeminiThinkingConfig
                {
                    ThinkingLevel = _config.ThinkingLevel,
                    ThinkingBudget = _config.ThinkingBudget
                };
    }
}
