namespace RAG.Interface
{
    /// <summary>Một kết quả chuẩn hóa được lưu xuống đĩa.</summary>
    /// <param name="Unchanged">
    /// Kết quả có giống hệt câu gốc hay không. BẮT BUỘC phải lưu: bộ chuẩn hóa fail-open bằng cách
    /// trả về nguyên câu gốc khi LLM lỗi, không phân biệt được với trường hợp câu vốn đã chuẩn.
    /// Mất cờ này thì một lần lỗi tạm thời sẽ được đóng băng xuống đĩa với thời hạn dài và sống qua
    /// mọi lần khởi động lại.
    /// </param>
    public sealed record StoredNormalization(string Question, string Normalized, bool Unchanged);

    /// <summary>Một vector embedding được lưu xuống đĩa, khóa theo chính đoạn text đã nhúng.</summary>
    public sealed record StoredEmbedding(string Text, float[] Vector);

    /// <summary>Ảnh chụp nội dung cache tại một thời điểm.</summary>
    public sealed record QueryCacheSnapshot(
        IReadOnlyList<StoredNormalization> Normalizations,
        IReadOnlyList<StoredEmbedding> Embeddings);

    /// <summary>
    /// Nơi lưu cache hỏi đáp xuống đĩa để nó sống qua khởi động lại và qua việc tạo lại container.
    /// <para>
    /// Tách khỏi các interface cache có chủ đích: cache lo phần tra cứu trong RAM, còn kho này
    /// lo phần bền vững. Nhờ vậy đổi sang Redis hay S3 chỉ phải thay implementation ở đây.
    /// </para>
    /// </summary>
    public interface IQueryCacheStore
    {
        /// <summary>
        /// Nạp snapshot đã lưu. Trả <c>null</c> khi chưa có file, file hỏng, hoặc vân tay không khớp
        /// (model hay số chiều đã đổi). Mọi lỗi I/O đều được nuốt — đây chỉ là tối ưu hóa.
        /// </summary>
        Task<QueryCacheSnapshot?> LoadAsync(string fingerprint, CancellationToken cancellationToken = default);

        /// <summary>Ghi đè toàn bộ snapshot. Trả <c>false</c> khi không ghi được.</summary>
        Task<bool> SaveAsync(string fingerprint, QueryCacheSnapshot snapshot, CancellationToken cancellationToken = default);
    }
}
