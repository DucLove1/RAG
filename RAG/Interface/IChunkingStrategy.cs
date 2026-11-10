namespace RAG.Interface
{
    /// <summary>
    /// Một đoạn đã cắt, kèm vị trí của nó trong tài liệu gốc.
    /// </summary>
    /// <param name="Ordinal">
    /// Số thứ tự 1-based của đoạn trong tài liệu, hoặc <c>null</c> khi chiến lược cắt không ánh xạ
    /// được đoạn về một đơn vị đếm được.
    /// <para>
    /// <c>null</c> nghĩa là đoạn này KHÔNG có mã chunk, nên không node hay cạnh nào của đồ thị nối
    /// tới nó được. Đó chính là lý do bật đồ thị tri thức bắt buộc phải dùng chiến lược cắt theo
    /// dòng.
    /// </para>
    /// </param>
    /// <param name="Stage">
    /// Giai đoạn mạch chuyện mà dòng này chỉ nên hiển thị khi khớp, lấy từ tiền tố
    /// <c>[stage:...]</c> ở đầu dòng gốc (xem <see cref="RAG.Class.Ingestion.LineChunker"/>).
    /// <c>null</c> khi dòng không có tiền tố — nghĩa là luôn hiển thị, bất kể request có gửi
    /// stage gì.
    /// </param>
    public sealed record TextSegment(string Text, int? Ordinal, string? Stage = null);

    /// <summary>
    /// Cắt một văn bản dài thành các đoạn để nhúng.
    /// <para>
    /// Là interface chứ không phải lớp static như bản trước: cách cắt đoạn ảnh hưởng trực tiếp tới
    /// chất lượng truy hồi, nên đây đúng là thứ sẽ được thay đi thử lại nhiều lần. Static thì không
    /// thay được cài đặt mà không sửa nơi gọi.
    /// </para>
    /// </summary>
    public interface IChunkingStrategy
    {
        IEnumerable<TextSegment> Chunk(string text);
    }
}
