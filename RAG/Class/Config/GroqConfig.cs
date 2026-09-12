using RAG.Class.Constants;
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

        /// <summary>
        /// Nhiệt độ (temperature) cho mô hình sinh văn bản. Giá trị từ 0 đến 1, càng cao thì kết quả càng sáng tạo, 
        /// càng thấp thì kết quả càng chính xác.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [Range(0, 1)]
        public float Temperature { get; set; } = 0.5f;

        [Required(AllowEmptyStrings = false)]
        [Range(1, 3000)]
        public int MaxOutputTokens { get; set; } = 400;

        /// <summary>
        /// Mức suy nghĩ gửi lên dưới dạng reasoning_effort. Để trống thì không gửi, model chạy theo mặc định.
        /// Token suy nghĩ vẫn tính vào <see cref="MaxOutputTokens"/>, nên mức thấp giúp câu trả lời không bị cắt cụt.
        /// </summary>
        /// <summary>
        /// Hạn thời gian cho CẢ một luồng streaming, tính từ lúc gửi request tới token cuối.
        /// <para>
        /// Tồn tại riêng vì đường không streaming KHÔNG có mục nào tương đương — ở đó SDK tự quản
        /// hạn thời gian, và đó là lựa chọn đúng cho một lời gọi duy nhất. Còn một luồng thì phải
        /// có trần của riêng nó: token đầu ra nhanh nhưng model kẹt ở giữa sẽ giữ kết nối mở vô
        /// hạn, và người chơi ngồi nhìn một câu thoại không bao giờ kết thúc.
        /// </para>
        /// <para>
        /// Đặt bằng một <c>CancellationTokenSource</c> nối với token của caller chứ KHÔNG đặt vào
        /// <c>OpenAIClientOptions</c>: đối tượng client được cache và dùng CHUNG với đường không
        /// streaming, nên chỉnh ở đó là âm thầm đổi hành vi của <c>api/query/ask</c>.
        /// </para>
        /// </summary>
        [Range(1, 600)]
        public int StreamTimeoutSeconds { get; set; } = 120;

        public GroqReasoningEffort? ReasoningEffort { get; set; }

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
