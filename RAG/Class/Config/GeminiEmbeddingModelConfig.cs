using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    public class GeminiEmbeddingModelConfig
    {
        public const string SectionName = "Gemini";
        [Required(AllowEmptyStrings = false)]
        public string ApiKey { get; set; } = string.Empty;
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string Url { get; set; } = string.Empty;
        [Required(AllowEmptyStrings = false)]
        public string Model { get; set; } = string.Empty;
        [Range(1, 4096)]
        public int OutputDimensions { get; set; } = 768;

        /// <summary>
        /// Hạn thời gian cho mỗi lời gọi. Mặc định của <c>HttpClient</c> là 100 giây — quá dài cho
        /// một request đang có người chơi ngồi chờ ở đầu bên kia.
        /// </summary>
        [Range(1, 600)]
        public int TimeoutSeconds { get; set; } = 30;


        /// <summary>
        /// URL tuyệt đối của endpoint batchEmbedContents (đối xứng với <see cref="Url"/>).
        /// Để trống thì provider tự động nhúng từng câu một — chậm hơn nhưng luôn chạy được.
        /// </summary>
        public string BatchUrl { get; set; } = string.Empty;

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
    }
}
