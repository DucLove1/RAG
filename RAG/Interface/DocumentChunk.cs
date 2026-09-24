namespace RAG.Interface
{
    /// <summary>
    /// Một đoạn văn bản đã cắt, sẵn sàng để nhúng và ghi vào kho vector.
    /// <para>
    /// Thay cho tuple <c>(string npcNames, string text, string? source)</c> dùng trước đây: tuple
    /// không đặt được tên trong chữ ký interface, nên người đọc phải mở implementation mới biết
    /// thứ tự ba chuỗi. Ba chuỗi cùng kiểu cạnh nhau là chỗ rất dễ hoán vị nhầm mà compiler im lặng.
    /// </para>
    /// </summary>
    /// <param name="NpcNames">NPC được biết đoạn này; đồng thời là khóa lọc khi truy hồi.</param>
    /// <param name="Text">Nội dung đoạn.</param>
    /// <param name="Source">Tên file gốc, chỉ để truy vết.</param>
    /// <param name="ChunkCode">
    /// Mã chunk <c>&lt;doc_id&gt;#L&lt;n&gt;</c>, hoặc <c>null</c> khi chiến lược cắt không sinh ra
    /// được. Đây là khóa ghép với đồ thị tri thức.
    /// </param>
    /// <param name="Metadata">Siêu dữ liệu của tài liệu chứa đoạn này, đi thẳng vào payload.</param>
    public sealed record DocumentChunk(string NpcNames,
                                       string Text,
                                       string? Source,
                                       string? ChunkCode,
                                       IReadOnlyDictionary<string, string> Metadata);
}
