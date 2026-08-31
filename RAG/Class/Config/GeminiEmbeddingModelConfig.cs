using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    public class GeminiEmbeddingModelConfig : IValidatableObject
    {
        public const string SectionName = "EmbeddingModel";

        /// <summary>Danh sách API key, sử dụng cái đầu tiên rồi xoay sang cái tiếp theo nếu bị 429.</summary>
        public List<string> ApiKeys { get; set; } = new();

        /// <summary>Base url, ví dụ: https://generativelanguage.googleapis.com/v1beta</summary>
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Tên model, ví dụ <c>models/gemini-embedding-2</c>. Lưu ý tiền tố <c>models/</c> là BẮT
        /// BUỘC vì giá trị này còn đi thẳng vào body của request; hai template đường dẫn dưới đây
        /// vì thế không tự thêm tiền tố đó.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Model { get; set; } = string.Empty;

        /// <summary>Đường dẫn tương đối tới endpoint nhúng một câu; {0} là tên model.</summary>
        [Required(AllowEmptyStrings = false)]
        public string EmbedContentPathTemplate { get; set; } = "{0}:embedContent";

        /// <summary>
        /// Đường dẫn tương đối tới endpoint nhúng theo lô; {0} là tên model.
        /// Để trống thì provider tự động nhúng từng câu một — chậm hơn nhưng luôn chạy được.
        /// </summary>
        public string BatchEmbedContentPathTemplate { get; set; } = "{0}:batchEmbedContents";

        /// <summary>Đường dẫn endpoint nhúng một câu, dựng từ <see cref="Model"/>.</summary>
        public string BuildEmbedContentPath() => string.Format(EmbedContentPathTemplate, Model);

        /// <summary>Đường dẫn endpoint nhúng theo lô, dựng từ <see cref="Model"/>.</summary>
        public string BuildBatchEmbedContentPath() => string.Format(BatchEmbedContentPathTemplate, Model);

        [Range(1, 4096)]
        public int OutputDimensions { get; set; } = 768;

        /// <summary>
        /// Hạn thời gian cho mỗi lời gọi. Mặc định của <c>HttpClient</c> là 100 giây — quá dài cho
        /// một request đang có người chơi ngồi chờ ở đầu bên kia.
        /// </summary>
        [Range(1, 600)]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Số câu tối đa mỗi lần gọi batch.
        /// Lưu ý: Google tính MỖI CÂU trong lô là một request đối với hạn mức embed, nên giá trị này
        /// phải nhỏ hơn hạn mức mỗi phút của gói đang dùng (free tier hiện là 100), chứ không phải
        /// đặt bằng giới hạn kỹ thuật của endpoint.
        /// </summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// Khoảng nghỉ giữa hai lô liên tiếp, để tổng số request trong một phút không vượt hạn mức.
        /// Đặt 0 khi dùng gói trả phí có hạn mức cao.
        /// </summary>
        public int BatchDelaySeconds { get; set; } = 60;

        /// <summary>
        /// Số ký tự tối đa của body lỗi được ghi vào log. Body lỗi của Google có thể rất dài,
        /// và log đầy một stack trace JSON thì không ai đọc nữa.
        /// </summary>
        public int ErrorBodyLogLimit { get; set; } = 500;

        /// <summary>
        /// Thời gian chờ (giây) trước khi thử lại một API key sau khi nó bị rate limit (429).
        /// Mặc định 60s tương ứng với cửa sổ rate-limit-per-minute của Gemini Embedding.
        /// </summary>
        [Range(1, 3600)]
        public int RateLimitCooldownSeconds { get; set; } = 60;

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            // Url TRƯỚC ĐÂY là URL tuyệt đối đã kèm ":embedContent". Giờ nó là base url, còn phần
            // ":embedContent" do template dựng ra. Giá trị cũ (thường còn sót trong biến môi trường
            // trên Render) sẽ tạo URL nối đôi và mọi lần nhúng đều 404 — nổ ngay lúc khởi động kèm
            // chỉ dẫn thì rẻ hơn nhiều so với đi dò một lỗi 404 câm.
            if (Url.Contains(':' + "embedContent", StringComparison.OrdinalIgnoreCase) ||
                Url.Contains(':' + "batchEmbedContents", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationResult(
                    $"EmbeddingModel:Url phải là BASE URL (ví dụ https://generativelanguage.googleapis.com/v1beta), " +
                    $"không phải URL đầy đủ như hiện tại (\"{Url}\"). Tên model giờ khai một lần ở " +
                    "EmbeddingModel:Model, còn đuôi endpoint nằm ở EmbeddingModel:EmbedContentPathTemplate và " +
                    "EmbeddingModel:BatchEmbedContentPathTemplate. Xóa biến môi trường EMBEDDINGMODEL__URL và " +
                    "EMBEDDINGMODEL__BATCHURL nếu chúng còn được đặt.",
                    new[] { nameof(Url) });
            }

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
