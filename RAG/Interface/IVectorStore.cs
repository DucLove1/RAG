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
    /// Bộ lọc payload đi kèm truy vấn. Mọi điều kiện trong <see cref="Must"/> phải cùng đúng
    /// (AND); <see cref="StageField"/>/<see cref="StageValues"/> thêm đúng MỘT điều kiện kiểu khác:
    /// trường đó phải RỖNG (không gắn) HOẶC khớp MỘT TRONG các giá trị — xem <see cref="WithStage"/>.
    /// </summary>
    public sealed record VectorSearchFilter(IReadOnlyList<PayloadMatchCondition> Must,
                                            string? StageField = null,
                                            IReadOnlyList<string>? StageValues = null)
    {
        public static VectorSearchFilter None { get; } = new(Array.Empty<PayloadMatchCondition>());

        public static VectorSearchFilter Match(string field, string value) =>
            new(new[] { new PayloadMatchCondition(field, value) });

        /// <summary>
        /// Thêm điều kiện "trường <paramref name="field"/> rỗng HOẶC khớp một trong
        /// <paramref name="values"/>", giữ nguyên mọi điều kiện <see cref="Must"/> đã có.
        /// <para>
        /// "Rỗng" ở đây nghĩa là payload không có khóa đó — đúng hệt những chunk không bao giờ
        /// gắn tiền tố <c>[stage:...]</c>. Nhờ vậy một NPC không dùng tính năng stage không bị
        /// ảnh hưởng gì khi filter này được áp lên: toàn bộ chunk của NPC đó rỗng trường stage,
        /// nên nhánh "rỗng" của OR luôn đúng.
        /// </para>
        /// <para>
        /// Nhận DANH SÁCH giá trị để diễn đạt luật tích lũy: ở stage 3 truyền <c>["1","2","3"]</c>.
        /// Danh sách rỗng là hợp lệ và nghĩa là chỉ còn các dòng không gắn stage.
        /// </para>
        /// </summary>
        public VectorSearchFilter WithStage(string field, IReadOnlyList<string> values) =>
            this with { StageField = field, StageValues = values };
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
