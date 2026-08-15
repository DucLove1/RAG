using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using RAG.Class.Config;
using RAG.Interface;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAG.Class
{
    public class GeminiEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiEmbeddingModelConfig _config;

        public GeminiEmbeddingProvider(HttpClient httpClient, IOptions<GeminiEmbeddingModelConfig> cfg)
        {
            _config = cfg.Value;
            _httpClient = httpClient;
            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_config.Url);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", $"{_config.ApiKey}");
        }

        public async Task<int> GetDimsAsync()
        {
            return _config.OutputDimensions;
        }

        public async Task<float[]> GetEmbeddingsAsync(string input, CancellationToken cancellationToken = default)
        {
            // Cấu hình Body theo đúng chuẩn quy định của Google AI Studio
            var requestBody = new GoogleEmbeddingRequest
            {
                Model = _config.Model,
                Content = new GoogleContent
                {
                    Parts = new[] { new GooglePart { Text = input } }
                },
                OutputDimensionality = _config.OutputDimensions
            };

            var response = await _httpClient.PostAsJsonAsync(_config.Url, requestBody, cancellationToken);
            //response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GoogleEmbeddingResponse>(cancellationToken: cancellationToken);

            return result?.Embedding?.Values ?? Array.Empty<float>();
        }
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