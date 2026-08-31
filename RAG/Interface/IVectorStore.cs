namespace RAG.Interface
{
    /// <summary>Một điểm dữ liệu chuẩn bị ghi vào kho vector.</summary>
    public sealed record VectorRecord(Guid Id, float[] Vector, IReadOnlyDictionary<string, object> Payload);

    /// <summary>Một kết quả truy hồi, kèm điểm tương đồng.</summary>
    public sealed record VectorHit(Guid Id, float Score, IReadOnlyDictionary<string, string> Payload);

    /// <summary>
    /// Một điều kiện lọc: trường <paramref name="Field"/> phải khớp <paramref name="Value"/>.
    /// <para>
    /// Cố tình KHÔNG nói khớp theo kiểu gì (phrase, keyword, full-text). Đó là chi tiết của từng
    /// kho vector và được chọn qua cấu hình, chứ không phải điều mà tầng nghiệp vụ cần biết.
    /// </para>
    /// </summary>
    public sealed record PayloadMatchCondition(string Field, string Value);

    /// <summary>
    /// Bộ lọc payload đi kèm truy vấn. Mọi điều kiện phải cùng đúng (AND).
    /// </summary>
    public sealed record VectorSearchFilter(IReadOnlyList<PayloadMatchCondition> Must)
    {
        public static VectorSearchFilter None { get; } = new(Array.Empty<PayloadMatchCondition>());

        public static VectorSearchFilter Match(string field, string value) =>
            new(new[] { new PayloadMatchCondition(field, value) });
    }

    /// <summary>
    /// Kho vector của ứng dụng.
    /// <para>
    /// Tên interface cố tình KHÔNG mang tên nhà cung cấp, và toàn bộ chữ ký chỉ dùng kiểu của
    /// ứng dụng. Bản trước (<c>IQdrantProvider</c>) nhận thẳng <c>Qdrant.Client.Grpc.Filter</c>
    /// và <c>using static RAG.Class.QdrantProvider</c> — tức là interface phụ thuộc vào
    /// implementation, đúng chiều ngược lại của DIP; hệ quả là pipeline phải tự dựng <c>Filter</c>
    /// của gRPC mới gọi được truy hồi.
    /// </para>
    /// </summary>
    public interface IVectorStore
    {
        /// <param name="dimension">Số chiều vector, lấy từ nhà cung cấp embedding.</param>
        Task CreateCollectionAsync(ulong dimension, CancellationToken cancellationToken = default);

        /// <summary>Đảm bảo collection tồn tại. Cài đặt nên chỉ chạm mạng đúng một lần.</summary>
        Task EnsureCollectionExistsAsync(ulong dimension, CancellationToken cancellationToken = default);

        Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<VectorHit>> SearchAsync(float[] queryVector,
                                                   VectorSearchFilter filter,
                                                   int topK,
                                                   CancellationToken cancellationToken = default);
    }
}
