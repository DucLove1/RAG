namespace RAG.Interface
{
    /// <summary>
    /// Một thực thể trong dữ liệu đồ thị viết tay.
    /// <para>
    /// <paramref name="Properties"/> nhận <c>object</c> chứ không phải <c>string</c> vì Neo4j phân
    /// biệt một chuỗi với một danh sách chuỗi, và có đúng một trường mà khác biệt đó là quan trọng:
    /// <c>vai_tro</c>. Ép nó thành chuỗi sẽ biến <c>["nhan_chung"]</c> thành <c>"nhan_chung"</c>, và
    /// mọi truy vấn kiểm vai trò bằng <c>IN</c> lặng lẽ không khớp nữa — không lỗi, chỉ sai.
    /// Giá trị hợp lệ chỉ gồm <c>string</c> và <c>List&lt;string&gt;</c>.
    /// </para>
    /// </summary>
    public sealed record GraphEntityData(string Name,
                                         IReadOnlyList<string> Labels,
                                         IReadOnlyList<string> Aliases,
                                         IReadOnlyList<string> ChunkCodes,
                                         IReadOnlyDictionary<string, object> Properties);

    /// <summary>
    /// Một quan hệ trong dữ liệu đồ thị viết tay. Loại ĐỐI XỨNG chỉ được khai MỘT chiều ở đây; bộ
    /// nạp tự phát chiều còn lại.
    /// <para>
    /// <paramref name="Properties"/> mang những gì làm một cạnh đọc ra thành câu người: <c>mo_ta</c>,
    /// <c>muc_do</c>, <c>theo_loi_khai_cua</c>, <c>vi_tri</c>, <c>moc_thoi_gian</c>. Thiếu nó thì
    /// prompt chỉ còn một cặp tên trần, và đó là phần lớn giá trị của tầng đồ thị so với RAG thuần.
    /// </para>
    /// </summary>
    public sealed record GraphRelationshipData(string From,
                                               string Type,
                                               string To,
                                               string Status,
                                               bool Negated,
                                               IReadOnlyList<string> ChunkCodes,
                                               IReadOnlyDictionary<string, object> Properties);

    public sealed record GraphData(IReadOnlyList<GraphEntityData> Entities,
                                   IReadOnlyList<GraphRelationshipData> Relationships);

    /// <summary>
    /// Đọc và KIỂM dữ liệu đồ thị viết tay.
    /// <para>
    /// Tách hẳn khỏi <see cref="IGraphLoader"/> và không hề biết Neo4j: nhờ vậy kiểm được dữ liệu
    /// mà không cần một database nào, và mọi phép kiểm chạy TRƯỚC khi có một dòng nào được ghi.
    /// </para>
    /// </summary>
    public interface IGraphDataSource
    {
        Task<GraphData> LoadAsync(CancellationToken cancellationToken = default);
    }

    /// <param name="SymmetricMirrored">Số cạnh ngược do bộ nạp tự phát ra từ loại đối xứng.</param>
    public sealed record GraphLoadReport(int Entities,
                                         int Relationships,
                                         int SymmetricMirrored,
                                         int AccessNodes,
                                         IReadOnlyList<string> Warnings);

    /// <param name="ExpectedRelationships">
    /// Số cạnh gold CỘNG số cạnh đối xứng. Thiếu vế sau là báo lệch oan ở mọi lần đối chiếu.
    /// </param>
    public sealed record GraphVerifyReport(int Entities,
                                           int Npcs,
                                           int Relationships,
                                           int ExpectedRelationships,
                                           IReadOnlyDictionary<string, int> ByRelationType,
                                           bool Matches);

    /// <summary>
    /// Nạp dữ liệu đồ thị lên Neo4j và đối chiếu.
    /// <para>
    /// Toàn bộ là MERGE, không có <c>DETACH DELETE</c> ở đâu trong codebase: chạy lại bao nhiêu lần
    /// cũng ra một kết quả. Cái giá là MERGE không bao giờ xóa — gỡ một cạnh khỏi file nguồn thì
    /// phải xóa tay, và <see cref="VerifyAsync"/> là thứ báo cho biết điều đó.
    /// </para>
    /// </summary>
    public interface IGraphLoader
    {
        Task<GraphLoadReport> LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>Đếm và đối chiếu, KHÔNG ghi gì.</summary>
        Task<GraphVerifyReport> VerifyAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Tạo constraint và index.
    /// <para>
    /// Tách khỏi <see cref="IGraphLoader"/> vì hai nhịp khác nhau: tạo index là việc chạy MỘT lần
    /// lúc dựng database, nạp dữ liệu là việc lặp lại. Trộn chung thì mỗi lần nạp đều phải đi qua
    /// đường tạo index, và constraint UNIQUE thì BẮT BUỘC phải có trước lần nạp đầu tiên.
    /// </para>
    /// </summary>
    public interface IGraphSchemaAdmin
    {
        Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
    }
}
