using System.ComponentModel.DataAnnotations;

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
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string Url { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string ApiKey { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string Model { get; set; } = string.Empty;

        /// <summary>Đường dẫn tương đối tới endpoint sinh nội dung; {0} là tên model.</summary>
        [Required(AllowEmptyStrings = false)]
        public string GenerateContentPathTemplate { get; set; } = "models/{0}:generateContent";

        public double Temperature { get; set; }

        public int MaxOutputTokens { get; set; }

        /// <summary>
        /// Hạn thời gian cho mỗi lời gọi. Mặc định của <c>HttpClient</c> là 100 giây — quá dài cho
        /// một request đang có người chơi ngồi chờ ở đầu bên kia.
        /// </summary>
        [Range(1, 600)]
        public int TimeoutSeconds { get; set; } = 30;


        public string BuildGenerateContentPath() =>
            string.Format(GenerateContentPathTemplate, Model);
    }
}
