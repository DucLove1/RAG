using RAG.Interface;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Null Object cho các interface cache: luôn báo trượt và không lưu gì.
    /// Được đăng ký khi QueryCache:Enabled = false, nhờ đó các decorator không cần biết đến cờ bật/tắt.
    /// </summary>
    public sealed class NullQueryCache : INormalizationCache, IEmbeddingCache, IRouteDecisionCache, IQueryCacheStatistics
    {
        public bool TryGetNormalizedQuestion(string question, out string normalized)
        {
            normalized = string.Empty;
            return false;
        }

        public void SetNormalizedQuestion(string question, string normalized, bool unchanged) { }

        public bool TryGetEmbedding(string text, out float[] vector)
        {
            vector = Array.Empty<float>();
            return false;
        }

        public void SetEmbedding(string text, float[] vector) { }

        public bool TryGetRoute(string question, out RouteMatch? route)
        {
            route = null;
            return false;
        }

        public void SetRoute(string question, RouteMatch? route) { }

        public QueryCacheStats GetStats() => new(0, 0, 0, 0, 0, 0);
    }
}
