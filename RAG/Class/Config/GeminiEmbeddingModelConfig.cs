namespace RAG.Class.Config
{
    public class GeminiEmbeddingModelConfig
    {
        public const string SectionName = "Gemini";
        public string ApiKey { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int OutputDimensions { get; set; } = 768;
    }
}
