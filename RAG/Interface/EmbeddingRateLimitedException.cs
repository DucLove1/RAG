namespace RAG.Interface
{
    /// <summary>
    /// Nhà cung cấp embedding đang giới hạn tần suất (HTTP 429).
    /// <para>
    /// Tách riêng khỏi các lỗi khác vì cách xử lý ngược nhau hoàn toàn: lỗi "endpoint không hỗ trợ"
    /// thì nên lùi về đường chậm hơn, còn lỗi giới hạn tần suất thì tuyệt đối KHÔNG được lùi —
    /// lùi về nhúng từng câu chính là nện thêm hàng trăm request vào một API đang từ chối.
    /// Đúng cách là dừng lại và thử lại sau.
    /// </para>
    /// </summary>
    public sealed class EmbeddingRateLimitedException : Exception
    {
        public EmbeddingRateLimitedException(string message) : base(message) { }
    }
}
