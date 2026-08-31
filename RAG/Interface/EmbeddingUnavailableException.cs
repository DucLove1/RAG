namespace RAG.Interface
{
    /// <summary>
    /// Nhà cung cấp embedding trả về lỗi (khác 429) hoặc không đọc được phản hồi.
    /// <para>
    /// Trước đây trường hợp này bị nuốt và trả về mảng rỗng. Vector rỗng KHÔNG phải là kết quả hợp lệ:
    /// nó đi thẳng vào truy hồi Qdrant và cho ra danh sách ngữ cảnh rác, rồi LLM dựng câu trả lời
    /// trên đống rác đó — sai hoàn toàn nhưng không có triệu chứng nào để người vận hành nhận ra.
    /// Ném ra để đường hỏi đáp trả 503 và người gọi biết là nên thử lại.
    /// </para>
    /// <para>
    /// Khác <see cref="EmbeddingRateLimitedException"/> ở cách xử lý: lỗi này CÓ THỂ lùi về nhúng
    /// từng câu ở đường batch, còn 429 thì tuyệt đối không.
    /// </para>
    /// </summary>
    public sealed class EmbeddingUnavailableException : Exception
    {
        public EmbeddingUnavailableException(string message) : base(message) { }

        public EmbeddingUnavailableException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
