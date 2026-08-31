using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Chẩn đoán định tuyến cho một câu hỏi. Chạy đúng những bước đầu của đường trả lời
    /// (chuẩn hóa + định tuyến) rồi dừng lại.
    /// <para>
    /// KHÔNG sinh câu trả lời và KHÔNG chạm kho vector. Chi phí phụ thuộc chiến lược đang chạy:
    /// với <c>Embedding</c> là một lần nhúng, với <c>Llm</c> là một lượt gọi mô hình — vẫn rẻ hơn
    /// nhiều so với chạy cả đường trả lời, nhưng không còn miễn phí như bản trước.
    /// </para>
    /// <para>
    /// Cố tình nhận <see cref="IRouteExplainer"/> chứ không phải <see cref="ISemanticRouter"/>: với
    /// chiến lược LLM, bản định tuyến đã bị bọc bởi cache, mà một chẩn đoán trả lời từ cache thì
    /// không còn là chẩn đoán — tinh chỉnh prompt xong hỏi lại vẫn ra kết quả cũ.
    /// </para>
    /// </summary>
    public sealed class RouteDiagnosticsService : IRouteDiagnostics
    {
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly IRouteExplainer _routeExplainer;

        public RouteDiagnosticsService(IQueryNormalizer queryNormalizer, IRouteExplainer routeExplainer)
        {
            _queryNormalizer = queryNormalizer;
            _routeExplainer = routeExplainer;
        }

        public async Task<RouteExplanation> ExplainRouteAsync(string question,
                                                              CancellationToken cancellationToken = default)
        {
            var normalizedQuestion = await _queryNormalizer.NormalizeAsync(question, cancellationToken);

            return await _routeExplainer.ExplainAsync(normalizedQuestion, cancellationToken);
        }
    }
}
