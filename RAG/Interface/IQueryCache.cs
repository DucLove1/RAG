namespace RAG.Interface
{
    /// <summary>
    /// Số liệu quan sát của cache. Không có nó thì không cách nào biết cache có thực sự hiệu quả
    /// hay chỉ đang tốn RAM.
    /// </summary>
    public sealed record QueryCacheStats(
        long NormalizationHits,
        long NormalizationMisses,
        long EmbeddingHits,
        long EmbeddingMisses)
    {
        public double NormalizationHitRate => Ratio(NormalizationHits, NormalizationMisses);
        public double EmbeddingHitRate => Ratio(EmbeddingHits, EmbeddingMisses);

        private static double Ratio(long hits, long misses) =>
            hits + misses == 0 ? 0d : (double)hits / (hits + misses);
    }

    /// <summary>
    /// Cache kết quả chuẩn hóa câu hỏi.
    /// <para>
    /// Đây là chỗ tiết kiệm lớn nhất trên mỗi request trúng cache: chuẩn hóa là một lần gọi LLM
    /// đầy đủ (~300-800ms), đắt hơn hẳn một lần gọi embedding.
    /// </para>
    /// </summary>
    public interface INormalizationCache
    {
        bool TryGetNormalizedQuestion(string question, out string normalized);

        /// <summary>
        /// Lưu kết quả chuẩn hóa.
        /// </summary>
        /// <param name="unchanged">
        /// Kết quả có giống hệt câu gốc hay không. Cần biết vì bộ chuẩn hóa fail-open: khi lỗi nó
        /// trả về nguyên câu gốc, không phân biệt được với trường hợp câu vốn đã chuẩn.
        /// Implementation nên cho nhóm này thời hạn ngắn hơn để một lần lỗi tạm thời không bị
        /// đóng băng thành vĩnh viễn.
        /// </param>
        void SetNormalizedQuestion(string question, string normalized, bool unchanged);
    }

    /// <summary>
    /// Cache vector embedding. Đánh thẳng vào nút thắt chính là hạn mức request mỗi phút
    /// của nhà cung cấp.
    /// </summary>
    public interface IEmbeddingCache
    {
        bool TryGetEmbedding(string text, out float[] vector);

        /// <summary>
        /// Lưu vector. Caller phải tự kiểm tra vector hợp lệ TRƯỚC khi gọi: cache một vector rỗng
        /// đồng nghĩa với việc câu đó vĩnh viễn không bao giờ khớp route nào.
        /// </summary>
        void SetEmbedding(string text, float[] vector);
    }

    /// <summary>
    /// Số liệu của cache, tách riêng vì đây là mối quan tâm của đường vận hành chứ không phải
    /// của các decorator. Bản trước gộp cả ba vai trò vào một interface, nên controller chỉ muốn
    /// đọc tỉ lệ trúng cũng phải phụ thuộc vào cả hai đường ghi (ISP).
    /// </summary>
    public interface IQueryCacheStatistics
    {
        QueryCacheStats GetStats();
    }

    /// <summary>
    /// Khả năng xuất/nạp snapshot để lưu cache xuống đĩa.
    /// <para>
    /// Tồn tại để <c>QueryCachePersistenceService</c> phụ thuộc vào một abstraction thay vì vào
    /// lớp <c>MemoryQueryCache</c> cụ thể như trước (DIP).
    /// </para>
    /// </summary>
    public interface IPersistableQueryCache
    {
        /// <summary>Vân tay của model + số chiều; đổi thì file cache cũ phải bị bỏ.</summary>
        string Fingerprint { get; }

        /// <summary>Số lần ghi, để service flush biết có gì mới đáng lưu hay không.</summary>
        long WriteCount { get; }

        /// <summary>Chụp lại N entry được dùng gần đây nhất của mỗi loại.</summary>
        QueryCacheSnapshot ExportSnapshot(int maxEntries);

        /// <summary>Nạp snapshot từ đĩa. Trả về số entry thực sự nạp được.</summary>
        int ImportSnapshot(QueryCacheSnapshot snapshot);
    }
}
