using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Chẩn đoán định tuyến cho một câu hỏi. Chạy đúng những bước đầu của đường trả lời
    /// (chuẩn hóa + nhúng) rồi dừng lại ở việc chấm điểm.
    /// <para>
    /// KHÔNG gọi LLM sinh câu trả lời và KHÔNG chạm kho vector, nên đây là vòng lặp rẻ nhất
    /// để tinh chỉnh ngưỡng.
    /// </para>
    /// </summary>
    public sealed class RouteDiagnosticsService : IRouteDiagnostics
    {
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly ISemanticRouter _semanticRouter;

        public RouteDiagnosticsService(IQueryNormalizer queryNormalizer,
                                       IEmbeddingProvider embeddingProvider,
                                       ISemanticRouter semanticRouter)
        {
            _queryNormalizer = queryNormalizer;
            _embeddingProvider = embeddingProvider;
            _semanticRouter = semanticRouter;
        }

        public async Task<RouteExplanation> ExplainRouteAsync(string question,
                                                              CancellationToken cancellationToken = default)
        {
            var normalizedQuestion = await _queryNormalizer.NormalizeAsync(question, cancellationToken);
            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(normalizedQuestion, cancellationToken);

            var scores = _semanticRouter.Explain(normalizedQuestion, questionEmbedding);
            var match = _semanticRouter.Route(normalizedQuestion, questionEmbedding);

            return new RouteExplanation(normalizedQuestion, scores, match);
        }
    }
}
