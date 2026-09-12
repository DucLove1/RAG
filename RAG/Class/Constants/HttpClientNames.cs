namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên của các named HttpClient được đăng ký qua IHttpClientFactory.
    /// </summary>
    public static class HttpClientNames
    {
        public const string GeminiLlm = nameof(GeminiLlm);

        /// <summary>
        /// Client RIÊNG cho đường streaming, chỉ khác client trên ở mỗi hạn thời gian.
        /// <para>
        /// Phải là một named client thứ hai chứ không phải chỉnh cái trên, vì ba lý do:
        /// <c>HttpClient.Timeout</c> tính cho TOÀN BỘ thao tác kể cả lúc đọc body — 30 giây sẽ
        /// giết đúng những câu trả lời dài, tức là phá đúng thứ streaming sinh ra; nó không sửa
        /// được nữa sau request đầu tiên (ném InvalidOperationException); và
        /// <c>IHttpClientFactory</c> phát ra instance DÙNG CHUNG, nên chỉnh theo từng request là
        /// chỉnh cho cả đường không streaming.
        /// </para>
        /// </summary>
        public const string GeminiLlmStream = nameof(GeminiLlmStream);

        public const string GeminiEmbedding = nameof(GeminiEmbedding);
    }
}
