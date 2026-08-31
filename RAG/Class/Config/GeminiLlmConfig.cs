using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình cho Gemini generateContent (LLM sinh văn bản), tách biệt hoàn toàn với
    /// <see cref="GeminiEmbeddingModelConfig"/> để mỗi config chỉ có một lý do thay đổi (SRP).
    /// </summary>
    public class GeminiLlmConfig : IValidatableObject
    {
        public const string SectionName = "GEMINILLM";

        /// <summary>Base url, ví dụ: https://generativelanguage.googleapis.com/v1beta</summary>
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string Url { get; set; } = string.Empty;

        /// <summary>Danh sách API key, sử dụng cái đầu tiên rồi xoay sang cái tiếp theo nếu bị 429.</summary>
        public List<string> ApiKeys { get; set; } = new();

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

        /// <summary>
        /// Thời gian chờ (giây) trước khi thử lại một API key sau khi nó bị rate limit (429).
        /// Mặc định 60s tương ứng với cửa sổ rate-limit-per-minute của Gemini.
        /// </summary>
        [Range(1, 3600)]
        public int RateLimitCooldownSeconds { get; set; } = 60;

        public string BuildGenerateContentPath() =>
            string.Format(GenerateContentPathTemplate, Model);

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (ApiKeys == null || ApiKeys.Count == 0)
                yield return new ValidationResult("ApiKeys không được trống.", new[] { nameof(ApiKeys) });
            else
            {
                foreach (var (key, i) in ApiKeys.Select((k, i) => (k, i)))
                {
                    if (string.IsNullOrWhiteSpace(key))
                        yield return new ValidationResult(
                            $"ApiKeys[{i}] không được trống hoặc chỉ chứa khoảng trắng.",
                            new[] { nameof(ApiKeys) });
                }
            }
        }
    }
}
