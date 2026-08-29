using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Null Object cho <see cref="IRouteVectorCache"/>: luôn báo cache miss và không ghi gì.
    /// Được đăng ký khi SemanticRouter:VectorCachePath để trống, nhờ đó warm-up không cần
    /// rẽ nhánh theo cờ bật/tắt cache.
    /// </summary>
    public sealed class NullRouteVectorCache : IRouteVectorCache
    {
        public Task<IReadOnlyDictionary<string, float[]>?> TryLoadAsync(string fingerprint, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, float[]>?>(null);

        public Task SaveAsync(string fingerprint, IReadOnlyDictionary<string, float[]> vectors, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
