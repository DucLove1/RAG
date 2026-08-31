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

        public string BuildUserPrompt(string question) =>
            string.Format(UserPromptTemplate, question);
    }
}
