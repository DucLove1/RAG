namespace RAG.Interface
{
    /// <summary>
    /// Một mối liên hệ lấy từ đồ thị, đã sẵn sàng để render vào prompt.
    /// <para>
    /// Hai đầu là TÊN chứ không phải id: đồ thị không chứa văn bản, thứ nó đóng góp cho prompt là
    /// tên thực thể và loại quan hệ. <paramref name="ChunkCodes"/> là đường truy ngược về nguyên văn
    /// ở nhánh vector — không có nó thì mô hình có một mối liên hệ mà không dẫn nguồn được.
    /// </para>
    /// </summary>
    public sealed record GraphRelation(string Source,
                                       string Relation,
                                       string Target,
                                       string Status,
                                       bool Negated,
                                       IReadOnlyList<string> ChunkCodes);

    /// <summary>
    /// Một lượt truy hồi trên đồ thị.
    /// <para>
    /// Chỉ mang câu hỏi, KHÔNG mang gì từ nhánh vector: hai nhánh chạy song song và độc lập, nên đồ
    /// thị tự chọn hạt giống từ câu hỏi. Đây cũng là đúng đầu vào mà global search sau này cần.
    /// </para>
    /// </summary>
    /// <param name="RelationBudget">Số cạnh tối đa; 0 thì dùng <c>Graph:Search:MaxRelations</c>.</param>
    public sealed record GraphSearchQuery(string NpcName,
                                          string Question,
                                          int RelationBudget);

    /// <summary>
    /// Kết quả một lượt truy hồi trên đồ thị.
    /// </summary>
    /// <param name="SupportingChunkCodes">
    /// Mã của những dòng mà các mối liên hệ trên tựa vào, cộng các dòng liền kề. Nhánh RAG tra ra
    /// nguyên văn để mô hình có cái mà trích dẫn.
    /// </param>
    /// <param name="Degraded">
    /// Đồ thị đang BẬT nhưng lượt tra này không lấy được gì vì sự cố (mạch hở, truy vấn hỏng, hết
    /// hạn). Khác hẳn "tra xong và không có liên hệ nào": cờ này chặn việc ghi một câu trả lời suy
    /// biến vào cache câu trả lời, nếu không thì một sự cố thoáng qua sẽ bị đóng băng lại và vẫn
    /// được phục vụ nguyên văn sau khi đồ thị đã sống lại.
    /// </param>
    /// <param name="Extraction">
    /// Hạt giống mà bước trích đã chọn, để chẩn đoán và ghi log. <c>null</c> khi không có bước trích
    /// nào chạy (đồ thị tắt).
    /// </param>
    public sealed record GraphContext(IReadOnlyList<GraphRelation> Relations,
                                      IReadOnlyList<string> SupportingChunkCodes,
                                      bool Degraded,
                                      GraphExtraction? Extraction = null)
    {
        public static GraphContext Empty { get; } =
            new(Array.Empty<GraphRelation>(), Array.Empty<string>(), Degraded: false);

        public static GraphContext DegradedEmpty { get; } =
            new(Array.Empty<GraphRelation>(), Array.Empty<string>(), Degraded: true);

        public bool IsEmpty => Relations.Count == 0;
    }

    /// <summary>
    /// Truy hồi trên đồ thị tri thức.
    /// <para>
    /// Tên cố ý KHÔNG phải <c>ILocalGraphSearch</c>: global search là một cài đặt khác của CÙNG
    /// interface này, không phải một interface mới. Đó là lý do kiểu trả về là
    /// <see cref="GraphContext"/> — một kiểu "đầu vào đã dựng sẵn cho prompt" — chứ không phải một
    /// danh sách cạnh thô: global search trả về bản tóm tắt cộng đồng chứ không trả về cạnh, mà
    /// tầng dựng ngữ cảnh phía trên thì không cần biết khác biệt đó.
    /// </para>
    /// <para>
    /// Cài đặt PHẢI nuốt mọi lỗi và trả <see cref="GraphContext.DegradedEmpty"/>. Đồ thị chết là
    /// chuyện phải vô hình với người chơi: câu trả lời tụt về RAG thuần, không phải về 500.
    /// </para>
    /// </summary>
    public interface IGraphSearch
    {
        Task<GraphContext> SearchAsync(GraphSearchQuery query, CancellationToken cancellationToken = default);
    }
}
