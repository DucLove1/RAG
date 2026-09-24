namespace RAG.Interface
{
    /// <summary>Danh sách NPC được biết một tài liệu.</summary>
    public sealed record DocumentAccess(string DocId, IReadOnlyList<string> NpcNames);

    /// <summary>
    /// Suy ra phân quyền tri thức từ siêu dữ liệu của corpus.
    /// <para>
    /// Tồn tại như một interface riêng vì nó là NGUỒN SỰ THẬT DUY NHẤT của phân quyền, và phải được
    /// dùng chung cho CẢ HAI nhánh: payload <c>npcNames</c> trong Qdrant và <c>duoc_biet</c> của
    /// NPC trong đồ thị. Hai bên tự tính lấy thì chỉ cần một bên sót là tri thức rò sang NPC khác,
    /// mà không có gì báo — câu truy vấn vẫn chạy, chỉ trả về nhiều hơn.
    /// </para>
    /// <para>
    /// Nhận CẢ TẬP tài liệu chứ không nhận từng tài liệu một, và đó là điều kiện bắt buộc: tài liệu
    /// bối cảnh thuộc về MỌI NPC, mà danh sách "mọi NPC" chỉ dựng được sau khi đã đọc hết các tài
    /// liệu khác. Chữ ký theo từng tài liệu sẽ không bao giờ biểu diễn nổi luật đó.
    /// </para>
    /// </summary>
    public interface IAccessPolicy
    {
        IReadOnlyList<DocumentAccess> Resolve(IReadOnlyList<IReadOnlyDictionary<string, string>> documents);
    }
}
