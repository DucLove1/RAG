using RAG.Class.Constants;
using System.Text.Json.Serialization;

namespace RAG.Class.Dto
{
    // --- DTO map theo chuẩn Interactions API (POST /v1beta/interactions) của Google Generative Language API ---

    public record GeminiInteractionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("system_instruction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SystemInstruction { get; init; }

        [JsonPropertyName("input")]
        public string Input { get; init; } = string.Empty;

        [JsonPropertyName("generation_config")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiGenerationConfig? GenerationConfig { get; init; }

        [JsonPropertyName("stream")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Stream { get; init; }
    }

    public record GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("max_output_tokens")]
        public int MaxOutputTokens { get; init; }

        [JsonPropertyName("thinking_level")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiThinkingLevel? ThinkingLevel { get; init; }
    }

    public record GeminiInteractionResponse
    {
        [JsonPropertyName("steps")]
        public GeminiInteractionStep[] Steps { get; init; } = Array.Empty<GeminiInteractionStep>();
    }

    public record GeminiInteractionStep
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public GeminiInteractionContent[] Content { get; init; } = Array.Empty<GeminiInteractionContent>();
    }

    public record GeminiInteractionContent
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    /// <summary>Một khung SSE của đường streaming, ví dụ <c>{"event_type":"step.delta","delta":{"type":"text","text":"..."}}</c>.</summary>
    public record GeminiInteractionStreamEvent
    {
        [JsonPropertyName("event_type")]
        public string EventType { get; init; } = string.Empty;

        [JsonPropertyName("delta")]
        public GeminiInteractionContent? Delta { get; init; }
    }
}
