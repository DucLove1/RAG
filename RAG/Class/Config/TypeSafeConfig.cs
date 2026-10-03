using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Kết nối tới TypeSafe System One API (mô hình quyết định Jev).
    /// <para>
    /// Chỉ được bind và validate khi <c>SemanticRouter:Strategy = Jev</c>: chạy chiến lược khác thì
    /// không cần key TypeSafe, và thiếu key không được phép làm app từ chối khởi động.
    /// </para>
    /// </summary>
    public class TypeSafeConfig : IValidatableObject
    {
        public const string SectionName = "TYPESAFE";

        /// <summary>Danh sách API key, sử dụng cái đầu tiên rồi xoay sang cái tiếp theo nếu bị 429.</summary>
        public List<string> ApiKeys { get; set; } = new();

        /// <summary>Base url, ví dụ: https://api.typesafe.ai</summary>
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string Url { get; set; } = "https://api.typesafe.ai";

        /// <summary>Đường dẫn tương đối tới endpoint System One.</summary>
        [Required(AllowEmptyStrings = false)]
        public string SystemOnePath { get; set; } = "v1/systemone";

        /// <summary>
        /// Phiên bản model. Nên ghim phiên bản cụ thể thay vì <c>jev-latest</c>: ngưỡng
        /// <c>SemanticRouter:Jev:MinConfidence</c> được đo trên một phiên bản, và alias trôi sang
        /// phiên bản mới sẽ lặng lẽ làm lệch ngưỡng đó.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Model { get; set; } = "jev-1.13.0";

        /// <summary>
        /// Hạn thời gian cho mỗi lời gọi. Router nằm trên đường nóng của mọi request và fail-open,
        /// nên chờ lâu chẳng được gì: quá hạn thì đi đường RAG luôn.
        /// </summary>
        [Range(1, 600)]
        public int TimeoutSeconds { get; set; } = 5;

        /// <summary>Số ký tự tối đa của body lỗi được ghi vào log.</summary>
        [Range(1, int.MaxValue)]
        public int ErrorBodyLogLimit { get; set; } = 500;

        /// <summary>Thời gian chờ (giây) trước khi thử lại một API key sau khi nó bị rate limit (429).</summary>
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
