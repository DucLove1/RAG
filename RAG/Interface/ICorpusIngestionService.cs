namespace RAG.Interface
{
    /// <summary>Một đoạn đã cắt kèm mã chunk, dùng để đối chiếu với file nguồn.</summary>
    public sealed record CorpusChunkPreview(string ChunkCode, string Text);

    /// <summary>Toàn bộ đoạn cắt ra từ một tài liệu, chưa nhúng và chưa ghi.</summary>
    public sealed record CorpusDocumentPreview(string DocId,
                                               string FileName,
                                               IReadOnlyList<string> NpcNames,
                                               IReadOnlyDictionary<string, string> Metadata,
                                               IReadOnlyList<CorpusChunkPreview> Chunks);

    /// <summary>Một tài liệu của corpus và danh sách NPC được biết nó.</summary>
    public sealed record CorpusDocumentReport(string DocId, string FileName, IReadOnlyList<string> NpcNames);

    /// <summary>
    /// Kết quả nạp cả corpus. Mang kèm bảng phân quyền để người vận hành đối chiếu ngay bằng mắt
    /// với bảng "Nhân vật dùng file nào" trong HUONG_DAN.md — phân quyền là thứ hỏng lặng lẽ nhất trong cả hệ thống, nên
    /// nó phải hiện ra ở đúng lúc người ta vừa bấm nạp.
    /// </summary>
    public sealed record CorpusIngestionReport(IngestionResult Result, IReadOnlyList<CorpusDocumentReport> Access);

    /// <summary>
    /// Nạp toàn bộ thư mục corpus trong một lệnh.
    /// <para>
    /// Tách khỏi <see cref="IIngestionService"/> theo ISP: kia là đường nạp TỔNG QUÁT nhận tài liệu
    /// từ bất kỳ đâu, còn đây là một thao tác vận hành gắn chặt với một thư mục cụ thể trên đĩa.
    /// Gộp vào một interface thì façade và các decorator đều phải cài thêm một method mà chúng
    /// không có việc gì để làm với nó.
    /// </para>
    /// <para>
    /// Tồn tại vì hai lý do thật, không phải vì tiện: phân quyền chỉ tính được khi nhìn CẢ TẬP tài
    /// liệu cùng lúc (tài liệu bối cảnh thuộc về mọi NPC, mà "mọi NPC" là hợp của các tài liệu
    /// khác), và một bài đo chỉ lặp lại được khi dựng lại index là một lệnh chứ không phải mười một
    /// lần đính kèm file thủ công.
    /// </para>
    /// </summary>
    public interface ICorpusIngestionService
    {
        Task<CorpusIngestionReport> IngestCorpusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Chỉ đọc và suy phân quyền, KHÔNG ghi gì. Dùng để soi bảng quyền trước khi nạp, hoặc khi
        /// nghi một NPC đang trả lời thứ không được biết.
        /// </summary>
        Task<IReadOnlyList<CorpusDocumentReport>> DescribeAccessAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Cắt đoạn và sinh mã chunk rồi DỪNG: không nhúng, không chạm kho vector, không tốn một
        /// lượt gọi API nào.
        /// <para>
        /// Tồn tại vì luật cắt dòng là hợp đồng với dữ liệu đồ thị viết tay, mà cách duy nhất để
        /// kiểm nó là so từng mã với đúng dòng trong file nguồn. Bắt người kiểm phải nạp thật mới
        /// xem được nghĩa là mỗi lần kiểm tốn một lượt nhúng cả corpus — và người ta sẽ thôi kiểm.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<CorpusDocumentPreview>> PreviewAsync(string? docId = null,
                                                                CancellationToken cancellationToken = default);
    }
}
