using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    public class GroqConfig : IValidatableObject
    {
        public const string SectionName = "GROQ";

        /// <summary>Danh sách API key, sử dụng cái đầu tiên rồi xoay sang cái tiếp theo nếu bị 429.</summary>
        public List<string> ApiKeys { get; set; } = new();

        [Required(AllowEmptyStrings = false)]
        public string Model { get; set; } = string.Empty;

        /// <summary>Endpoint tương thích OpenAI của Groq.</summary>
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string Url { get; set; } = "https://api.groq.com/openai/v1";

        /// <summary>
        /// Thời gian chờ (giây) trước khi thử lại một API key sau khi nó bị rate limit (429).
        /// Mặc định 60s tương ứng với cửa sổ rate-limit-per-minute của Groq.
        /// </summary>
        [Range(1, 3600)]
        public int RateLimitCooldownSeconds { get; set; } = 60;

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
