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
    /// Cache cho đường hỏi đáp: kết quả chuẩn hóa câu hỏi và vector embedding.
    /// <para>
    /// Cả hai đều là hàm thuần theo đầu vào (cùng một câu luôn cho ra cùng kết quả), nên cache là
    /// đúng đắn về mặt ngữ nghĩa. Trong game NPC, người chơi lặp lại câu hỏi rất nhiều
    /// ("xin chào", "cảm ơn"), và node chuẩn hóa còn gom mọi biến thể sai chính tả về một dạng
    /// duy nhất trước khi tới bước embedding — nên tỉ lệ trúng cache thực tế rất cao.
    /// </para>
    /// Tách thành abstraction riêng để sau này đổi sang Redis hay cache trên đĩa chỉ phải thay
    /// implementation, không đụng tới các decorator đang dùng nó.
    /// </summary>
    public interface IQueryCache
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

        bool TryGetEmbedding(string text, out float[] vector);

        /// <summary>
        /// Lưu vector. Caller phải tự kiểm tra vector hợp lệ TRƯỚC khi gọi: nhà cung cấp embedding
        /// trả mảng rỗng khi API lỗi thay vì ném exception, và cache một vector rỗng đồng nghĩa với
        /// việc câu đó vĩnh viễn không bao giờ khớp route nào.
        /// </summary>
        void SetEmbedding(string text, float[] vector);

        QueryCacheStats GetStats();
    }
}
