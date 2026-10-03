using RAG.Class.Constants;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình cho node chuẩn hóa câu hỏi (viết tắt, sai chính tả, thiếu dấu...).
    /// Toàn bộ prompt và ngưỡng đều nằm ở configuration để đổi hành vi không cần build lại.
    /// </summary>
    public class QueryNormalizationConfig
    {
        public const string SectionName = "QueryNormalization";

        /// <summary>Bật/tắt node. Khi tắt, pipeline dùng bản cài đặt passthrough.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Provider dùng để chuẩn hóa (mặc định Gemini, độc lập với provider trả lời).</summary>
        public LlmProviderKey Provider { get; set; } = LlmProviderKey.Gemini;

        /// <summary>
        /// Model riêng cho bước chuẩn hóa. Để trống thì dùng model mặc định của provider
        /// (<c>GEMINILLM:Model</c> hoặc <c>GROQ:Model</c> trong appsettings.json). Đặt giá trị ở đây để chuẩn hóa chạy
        /// model rẻ hơn model của những node khác mà vẫn dùng chung pool API key.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>Để trống thì không gửi, model chạy theo mức suy nghĩ mặc định của nhà cung cấp.</summary>
        public LlmThinkingLevel? ThinkingLevel { get; set; }

        /// <summary>System prompt mô tả nhiệm vụ chuẩn hóa.</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>Template user prompt; {0} là câu hỏi gốc.</summary>
        public string UserPromptTemplate { get; set; } = string.Empty;

        /// <summary>Câu hỏi dài hơn ngưỡng này sẽ bỏ qua bước chuẩn hóa (tiết kiệm chi phí).</summary>
        public int MaxInputLength { get; set; } = 512;

        /// <summary>
        /// Chặn trường hợp LLM "chém gió": nếu kết quả dài hơn câu gốc quá tỉ lệ này thì giữ câu gốc.
        /// </summary>
        public double MaxLengthRatio { get; set; } = 3.0;

        /// <summary>
        /// System prompt khi CÓ lịch sử hội thoại: chuẩn hóa như trên VÀ thay đại từ / tham chiếu
        /// ("hắn", "chỗ đó", "hôm ấy") bằng tên cụ thể lấy từ lịch sử, để câu đứng độc lập được.
        /// Để trống thì có lịch sử cũng chuẩn hóa như không có — câu nối tiếp sẽ truy hồi kém.
        /// </summary>
        public string HistorySystemPrompt { get; set; } = string.Empty;

        /// <summary>Template user prompt khi có lịch sử; {0} = bản ghi hội thoại, {1} = câu hỏi gốc.</summary>
        public string HistoryUserPromptTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Số lượt CUỐI đưa cho bộ chuẩn hóa (kèm tóm tắt nếu có). Tham chiếu gần như luôn trỏ về vài
        /// lượt vừa nói, nên đưa ít lượt giữ cho lượt gọi này rẻ.
        /// </summary>
        public int HistoryMaxTurns { get; set; } = 4;

        /// <summary>
        /// Trần độ dài TUYỆT ĐỐI của câu đã viết lại khi có lịch sử, thay cho <see cref="MaxLengthRatio"/>:
        /// câu nối tiếp thường rất ngắn ("thế còn hắn?", "ừ, rồi sao?") nên viết lại có thể dài gấp nhiều
        /// lần mà vẫn đúng — so theo tỉ lệ thì gần như mọi lần viết lại đều bị loại.
        /// </summary>
        public int HistoryMaxOutputLength { get; set; } = 300;

        /// <summary>Có đủ template để viết lại câu theo lịch sử không.</summary>
        public bool CanRewriteWithHistory =>
            !string.IsNullOrWhiteSpace(HistorySystemPrompt) &&
            HistoryUserPromptTemplate.Contains("{0}", StringComparison.Ordinal) &&
            HistoryUserPromptTemplate.Contains("{1}", StringComparison.Ordinal);

        public string BuildUserPrompt(string question) =>
            string.Format(UserPromptTemplate, question);

        public string BuildHistoryUserPrompt(string transcript, string question) =>
            string.Format(HistoryUserPromptTemplate, transcript, question);
    }
}
