using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;
using System.Net;
using System.Net.Http.Json;

namespace RAG.Class
{
    /// <summary>
    /// <see cref="ILLMProvider"/> dùng Gemini generateContent.
    /// Đăng ký kèm khóa <see cref="LlmProviderKey.Gemini"/> để sống chung với GroqCloudProvider.
    /// Luân chuyển API key khi bị 429 (rate limit).
    /// </summary>
    public class GeminiLLMProvider : ILLMProvider
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

        public async Task<string> AskAsync(string system, string user, string? model = null, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                try
                {
                    var key = _rotator.GetCurrentKey();
                    return await TryAskAsync(system, user, model, key, cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.InnerException is HttpRequestException &&
                                                     ex.Message.Contains("429"))
                {
                    var key = _rotator.GetCurrentKey();
                    _rotator.ReportRateLimited(key);
                    // Loop lại để thử key tiếp theo.
                }
                catch (AllApiKeysRateLimitedException)
                {
                    throw; // Mọi key đều hết quota, thôi.
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
        }

        private async Task<string> TryAskAsync(string system, string user, string? model, string apiKey, CancellationToken cancellationToken)
        {
            var request = new GeminiGenerateContentRequest
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
                    MaxOutputTokens = _config.MaxOutputTokens
                }
            };

            var httpClient = _httpClientFactory.CreateClient(HttpClientNames.GeminiLlm);

            // Tạo HttpRequestMessage để attach API key per request chứ không bake vào HttpClient.
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.BuildGenerateContentPath(model))
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Remove(GeminiApiDefaults.ApiKeyHeader);
            httpRequest.Headers.Add(GeminiApiDefaults.ApiKeyHeader, apiKey);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new HttpRequestException("Rate limit (429)");

            response.EnsureSuccessStatusCode();

            var payload = await response.Content
                .ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken: cancellationToken);

            var parts = payload?.Candidates.FirstOrDefault()?.Content?.Parts;
            if (parts is null || parts.Length == 0)
                return string.Empty;

            return string.Concat(parts.Select(part => part.Text)).Trim();
        }
    }
}
