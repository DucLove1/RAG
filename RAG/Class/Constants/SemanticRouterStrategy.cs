namespace RAG.Class.Constants
{
    /// <summary>
    /// Chiến lược định tuyến đang chạy. Dùng enum thay cho magic string để tránh sai chính tả
    /// và bind được trực tiếp từ configuration, giống <see cref="LlmProviderKey"/>.
    /// <para>
    /// Thay thế hẳn cờ <c>Enabled</c> cũ chứ không đứng cạnh nó: hai nguồn sự thật cho câu hỏi
    /// "router có đang bật không" chính là thứ mà <see cref="Off"/> sinh ra để dập tắt.
    /// </para>
    /// </summary>
    public enum SemanticRouterStrategy
    {
        /// <summary>Tắt định tuyến: mọi câu đều đi đường RAG (dùng Null Object).</summary>
        Off = 0,

        /// <summary>Định tuyến bằng cosine similarity trên vector câu mẫu nạp sẵn.</summary>
        Embedding = 1,

        /// <summary>Định tuyến bằng cách để LLM đọc câu hỏi và chọn nhãn route.</summary>
        Llm = 2
    }
}
