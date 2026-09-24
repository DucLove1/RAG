namespace RAG.Interface
{
    /// <summary>
    /// Một cạnh lấy từ đồ thị, ở dạng thô trước khi dựng thành ngữ cảnh.
    /// </summary>
    /// <param name="ElementId">
    /// Danh tính của cạnh trong Neo4j. Dùng để khử trùng ở tầng ứng dụng: mẫu truy vấn vô hướng có
    /// thể khớp cùng một cạnh hai lần khi CẢ HAI đầu của nó đều là hạt giống.
    /// </param>
    /// <param name="Source">Tên đầu NGUỒN theo chiều LƯU TRỮ, không phải theo phía hạt giống.</param>
    /// <param name="StatusPriority">Đã tính sẵn trong Cypher để thứ tự ở đây và ở đó không thể lệch nhau.</param>
    public sealed record GraphEdge(string ElementId,
                                   string Source,
                                   string Relation,
                                   string Target,
                                   string Status,
                                   bool Negated,
                                   IReadOnlyList<string> ChunkCodes,
                                   int StatusPriority,
                                   bool IsReasoning);

    /// <summary>Một dòng trong danh mục thực thể mà một NPC được biết.</summary>
    /// <param name="Labels">Nhãn loại (đã bỏ nhãn nền và nhãn NPC), sắp theo tên để prompt ổn định.</param>
    /// <param name="IsSelf">Dòng này là chính NPC đang được hỏi.</param>
    public sealed record GraphCatalogEntry(string Name,
                                           IReadOnlyList<string> Aliases,
                                           IReadOnlyList<string> Labels,
                                           bool IsSelf);

    /// <summary>
    /// Kết quả một lượt tra đồ thị.
    /// <para>
    /// <see cref="Failed"/> tách "đồ thị không trả lời được" khỏi "đồ thị trả lời là không có gì".
    /// Trả danh sách rỗng cho cả hai thì tầng trên không bao giờ biết mình đang suy biến, và câu
    /// trả lời sinh ra lúc Neo4j chết sẽ được ghi vào cache như một câu trả lời bình thường.
    /// </para>
    /// </summary>
    public sealed record GraphStoreResult<T>(IReadOnlyList<T> Items, bool Failed)
    {
        public static GraphStoreResult<T> Ok(IReadOnlyList<T> items) => new(items, Failed: false);

        public static GraphStoreResult<T> None { get; } = new(Array.Empty<T>(), Failed: false);

        public static GraphStoreResult<T> Unavailable { get; } = new(Array.Empty<T>(), Failed: true);
    }

    /// <param name="EntityNames">
    /// Tên CHUẨN của thực thể hạt giống, theo thứ tự trọng tâm. Thứ tự này là thứ hạng nhóm khi chia
    /// lượt cạnh, nên không được sắp lại.
    /// </param>
    /// <param name="IntentRelationTypes">Loại quan hệ câu hỏi muốn biết. Chỉ để ƯU TIÊN cạnh, không lọc.</param>
    /// <param name="ReasoningOnly">Chỉ giữ quan hệ mang lập luận. Xem <c>Graph:Search:ReasoningRelationsOnly</c>.</param>
    public sealed record GraphExpansionRequest(string NpcName,
                                               IReadOnlyList<string> EntityNames,
                                               IReadOnlyList<string> IntentRelationTypes,
                                               bool ReasoningOnly,
                                               int Limit);

    /// <summary>
    /// Truy cập đồ thị ở mức thô.
    /// <para>
    /// Tách khỏi <see cref="IGraphSearch"/> theo DIP: kia là CHIẾN LƯỢC truy hồi (local hôm nay,
    /// global về sau), đây là CÁCH nói chuyện với kho đồ thị. Global search sẽ là một cài đặt khác
    /// của <c>IGraphSearch</c> nhưng dùng lại nguyên lớp cài đặt interface này.
    /// </para>
    /// <para>
    /// BẤT BIẾN: mọi method ở đây KHÔNG BAO GIỜ ném, trừ khi chính token của caller bị hủy. Đồ thị
    /// chết phải vô hình với người chơi — câu trả lời tụt về RAG thuần, không phải về 500 — nhưng
    /// KHÔNG vô hình với tầng trên: lỗi và mạch hở trả <see cref="GraphStoreResult{T}.Unavailable"/>.
    /// </para>
    /// </summary>
    public interface IGraphStore
    {
        Task<GraphStoreResult<GraphEdge>> ExpandAsync(GraphExpansionRequest request,
                                                      CancellationToken cancellationToken = default);

        /// <summary>
        /// Danh mục thực thể NPC được biết, chịu cùng bộ lọc quyền với truy vấn mở rộng. NPC không
        /// tồn tại thì trả danh sách rỗng, không phải lỗi.
        /// </summary>
        Task<GraphStoreResult<GraphCatalogEntry>> ListKnownEntitiesAsync(string npcName,
                                                                         CancellationToken cancellationToken = default);
    }

    /// <summary>Số liệu vận hành của tầng đồ thị, cho endpoint chẩn đoán.</summary>
    public sealed record GraphStats(long Expansions, long EmptyResults, long Errors);

    /// <summary>
    /// Tách khỏi <see cref="IGraphStore"/> theo ISP: đường trả lời không bao giờ đọc số liệu, và
    /// đường chẩn đoán không bao giờ truy vấn đồ thị.
    /// </summary>
    public interface IGraphStatistics
    {
        GraphStats GetStats();
    }
}
