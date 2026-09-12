using RAG.Class.Constants;
using System.Text.Json.Serialization;

namespace RAG.Class.Dto
{
    // --- DTO map theo chuẩn generateContent của Google Generative Language API ---

    public record GeminiGenerateContentRequest
    {
        [JsonPropertyName("system_instruction")]
        public GeminiContent? SystemInstruction { get; init; }

        [JsonPropertyName("contents")]
        public GeminiContent[] Contents { get; init; } = Array.Empty<GeminiContent>();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; init; }
    }

    public record GeminiContent
    {
        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Role { get; init; }

        [JsonPropertyName("parts")]
        public GeminiPart[] Parts { get; init; } = Array.Empty<GeminiPart>();
    }

    public record GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    public record GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; init; }

        [JsonPropertyName("thinkingConfig")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiThinkingConfig? ThinkingConfig { get; init; }
    }

    public record GeminiThinkingConfig
    {
        [JsonPropertyName("thinkingLevel")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiThinkingLevel? ThinkingLevel { get; init; }

        [JsonPropertyName("thinkingBudget")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ThinkingBudget { get; init; }
    }

    public record GeminiGenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public GeminiCandidate[] Candidates { get; init; } = Array.Empty<GeminiCandidate>();
    }

    public record GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; init; }
    }
}
