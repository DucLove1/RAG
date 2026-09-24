namespace RAG.Interface
{
    /// <summary>
    /// Đổi cách corpus gọi một NPC (VAI TRÒ: <c>"Pháp y"</c>) sang TÊN RIÊNG mà người chơi và đồ thị
    /// dùng (<c>"Edward"</c>).
    /// <para>
    /// Tồn tại để cả hệ thống chỉ có MỘT cách gọi NPC: payload <c>npcNames</c> trong Qdrant,
    /// <c>duoc_biet</c> trong đồ thị và field <c>npcName</c> của mọi endpoint đều là tên riêng. Để
    /// hai kho gọi khác nhau thì một câu hỏi gửi bằng tên riêng sẽ bị Qdrant lọc sạch — không lỗi,
    /// chỉ là NPC "không biết gì".
    /// </para>
    /// </summary>
    public interface INpcNameResolver
    {
        /// <summary>Tên riêng của NPC; tên không nối được thì trả nguyên văn.</summary>
        string Resolve(string corpusName);
    }
}
