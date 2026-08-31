using System.Text.Json.Serialization;

namespace RAG.Class.Dto
{
    // --- DTO map theo chuẩn embedContent / batchEmbedContents của Google Generative Language API ---
    //
    // Cố tình KHÔNG đặt giá trị mặc định cho Model và OutputDimensionality: bản trước để sẵn
    // "models/text-embedding-004" và 1024 ngay trong DTO, nên một chỗ quên set là lặng lẽ gửi đi
    // model khác với model đang cấu hình — và vector nhận về vẫn "hợp lệ" nên không ai phát hiện.

    public record GoogleEmbeddingRequest
    {
        [JsonPropertyName("model")] public required string Model { get; init; }
        [JsonPropertyName("content")] public required GoogleContent Content { get; init; }
        [JsonPropertyName("output_dimensionality")] public required int OutputDimensionality { get; init; }
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
}
