using RAG.Interface;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Null Object cho tầng cache ngữ nghĩa: luôn báo trượt, không lưu gì, không xoá được gì.
    /// Được đăng ký khi <c>SemanticAnswerCache:Enabled = false</c>, nhờ đó <c>AskPipeline</c>
    /// không cần biết đến cờ bật/tắt — cùng vai trò với <see cref="NullQueryCache"/>.
    /// <para>
    /// Nằm ở đây chứ không trong thư mục của một provider cụ thể: nó được dùng khi cache TẮT, tức
    /// là khi chưa có provider nào được chọn. Để trong thư mục Redis thì nhánh FAISS sẽ phải
    /// <c>using RAG.Class.Caching.Redis</c> — đúng thứ mà luật cô lập muốn tránh.
    /// </para>
    /// </summary>
    public sealed class NullSemanticAnswerCache : ISemanticAnswerCache,
                                                  ISemanticAnswerCacheStatistics,
                                                  ISemanticAnswerCacheAdmin
    {
        private static readonly Task<CachedAnswer?> Miss = Task.FromResult<CachedAnswer?>(null);

        public Task<CachedAnswer?> TryGetAsync(SemanticAnswerQuery query, CancellationToken cancellationToken = default) => Miss;

        public Task SetAsync(SemanticAnswerQuery query, string answer, bool hasContext, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<long> PurgeAsync(string npcName, string npcPersona, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public SemanticAnswerCacheStats GetStats() => new(0, 0, 0, 0);
    }
}
