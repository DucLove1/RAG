namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình cho Gemini generateContent (LLM sinh văn bản), tách biệt hoàn toàn với
    /// <see cref="GeminiEmbeddingModelConfig"/> để mỗi config chỉ có một lý do thay đổi (SRP).
    /// </summary>
    public class GeminiLlmConfig
    {
        public const string SectionName = "GEMINILLM";

        /// <summary>Base url, ví dụ: https://generativelanguage.googleapis.com/v1beta</summary>
        public string Url { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        /// <summary>Đường dẫn tương đối tới endpoint sinh nội dung; {0} là tên model.</summary>
        public string GenerateContentPathTemplate { get; set; } = "models/{0}:generateContent";

        public double Temperature { get; set; }

        public int MaxOutputTokens { get; set; }

        public string BuildGenerateContentPath() =>
            string.Format(GenerateContentPathTemplate, Model);
    }
}
