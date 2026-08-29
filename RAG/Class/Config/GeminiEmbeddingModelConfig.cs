namespace RAG.Class.Config
{
    public class GeminiEmbeddingModelConfig
    {
        public const string SectionName = "Gemini";
        public string ApiKey { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int OutputDimensions { get; set; } = 768;

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
    }
}
