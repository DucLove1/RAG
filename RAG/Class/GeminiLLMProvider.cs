using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;
using System.Net.Http.Json;

namespace RAG.Class
{
    /// <summary>
    /// <see cref="ILLMProvider"/> dùng Gemini generateContent.
    /// Đăng ký kèm khóa <see cref="LlmProviderKey.Gemini"/> để sống chung với GroqCloudProvider.
    /// </summary>
    public class GeminiLLMProvider : ILLMProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GeminiLlmConfig _config;

        public GeminiLLMProvider(IHttpClientFactory httpClientFactory, IOptions<GeminiLlmConfig> options)
        {
            _httpClientFactory = httpClientFactory;
            _config = options.Value;
        }

        public async Task<string> AskAsync(string system, string user, CancellationToken cancellationToken = default)
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

            var response = await httpClient.PostAsJsonAsync(
                _config.BuildGenerateContentPath(), request, cancellationToken);

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
