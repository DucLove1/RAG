using RAG.Interface;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Null Object cho <see cref="IQueryCacheStore"/>: không nạp và không ghi gì.
    /// Được đăng ký khi QueryCache:PersistPath để trống, nhờ đó service flush không cần rẽ nhánh
    /// theo cờ bật/tắt lưu đĩa.
    /// </summary>
    public sealed class NullQueryCacheStore : IQueryCacheStore
    {
        public Task<QueryCacheSnapshot?> LoadAsync(string fingerprint, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueryCacheSnapshot?>(null);

        public Task<bool> SaveAsync(string fingerprint, QueryCacheSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
