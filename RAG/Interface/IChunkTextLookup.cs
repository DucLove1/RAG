namespace RAG.Interface
{
    /// <summary>Nguyên văn của một đoạn, tra theo mã chunk.</summary>
    public sealed record ChunkText(string Code, string Text, string Source);

    /// <summary>
    /// Tra nguyên văn theo mã chunk.
    /// <para>
    /// Tách khỏi <see cref="IVectorStore"/> theo ISP: đường trả lời cần đúng một việc là "cho tôi
    /// văn bản của mấy mã này", chứ không cần tạo collection, ghi điểm hay tìm theo vector. Cài đặt
    /// vẫn là cùng một lớp và cùng một instance.
    /// </para>
    /// </summary>
    public interface IChunkTextLookup
    {
        /// <param name="npcName">
        /// Bộ lọc quyền ở phía RAG. Đồ thị đã lọc bằng <c>duoc_biet</c> rồi, nhưng mã chunk quay về
        /// đây là dữ liệu do ĐỒ THỊ chọn — tin nó mà không lọc lại là mở một đường vòng qua bộ lọc
        /// NPC. Lọc hai lần rẻ hơn nhiều so với việc phải chứng minh đường vòng đó không tồn tại.
        /// </param>
        /// <param name="stage">
        /// Tên stage hiện tại; chỉ trả các dòng không gắn stage hoặc gắn stage ≤ stage này. Đồ thị đã
        /// bỏ mã của stage sau khỏi danh sách gửi tới đây, nhưng cùng lý do với bộ lọc quyền ở trên:
        /// mã đi qua nhiều tầng không được tin suông. <c>null</c> thì không lọc.
        /// </param>
        /// <returns>Chỉ chứa những mã tìm thấy. Mã không có trong kho đơn giản là vắng mặt.</returns>
        Task<IReadOnlyDictionary<string, ChunkText>> GetByCodesAsync(string npcName,
                                                                     IReadOnlyCollection<string> codes,
                                                                     string? stage = null,
                                                                     CancellationToken cancellationToken = default);
    }
}
