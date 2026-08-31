using RAG.Interface;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Decorator bọc quanh router thật để khỏi phân loại lại câu đã gặp.
    /// <para>
    /// Đây là tầng đắt nhất khi trượt: chiến lược định tuyến bằng LLM trả giá một lượt gọi mô hình
    /// đầy đủ (~300-800ms) cho mỗi lần trượt. Trong hội thoại NPC thì câu chào, câu cảm ơn và câu
    /// tạm biệt lặp lại rất nhiều, nên tỉ lệ trúng ở đây cao hơn hẳn hai tầng cache còn lại.
    /// </para>
    /// <para>
    /// Cache cả quyết định ÂM — "không route nào khớp" — là phần bắt buộc chứ không phải tối ưu
    /// thêm: câu hỏi tri thức chiếm phần lớn lưu lượng, và nếu chỉ cache quyết định dương thì đúng
    /// nhóm đông nhất phải trả giá một lượt gọi LLM mỗi lần. Quyết định âm được cho thời hạn ngắn
    /// hơn ở tầng cache, vì router fail-open nên nó có thể là kết luận thật hoặc là dấu vết của
    /// một lần lỗi.
    /// </para>
    /// <para>
    /// Khi <c>QueryCache:Enabled = false</c>, cache tiêm vào đây là Null Object luôn báo trượt, nên
    /// decorator tự thoái hóa thành passthrough mà không cần nhánh rẽ nào.
    /// </para>
    /// </summary>
    public sealed class CachingSemanticRouter : ISemanticRouter
    {
        private readonly ISemanticRouter _inner;
        private readonly IRouteDecisionCache _cache;
        private readonly ILogger<CachingSemanticRouter> _logger;

        public CachingSemanticRouter(ISemanticRouter inner,
                                     IRouteDecisionCache cache,
                                     ILogger<CachingSemanticRouter> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public async Task<RouteMatch?> RouteAsync(string question, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question))
                return await _inner.RouteAsync(question, cancellationToken);

            var key = question.Trim();

            if (_cache.TryGetRoute(key, out var cached))
            {
                _logger.LogDebug("Trúng cache định tuyến: {Question} -> {Route}", key, cached?.Name ?? "(RAG)");
                return cached;
            }

            var route = await _inner.RouteAsync(question, cancellationToken);

            _cache.SetRoute(key, route);

            return route;
        }
    }
}
