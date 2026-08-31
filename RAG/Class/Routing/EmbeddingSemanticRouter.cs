using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Định tuyến bằng cosine similarity trên các vector câu mẫu nạp sẵn trong RAM.
    /// <para>
    /// Lớp này CHỈ so khớp. Việc nạp vector nằm ở <see cref="RouteCatalogBuilder"/>, việc thêm câu
    /// mẫu lúc chạy nằm ở <see cref="RouteUtteranceAdmin"/>, quy tắc gộp điểm nằm ở
    /// <see cref="IRouteScorer"/>, và trạng thái dùng chung nằm ở <see cref="RouteCatalog"/>.
    /// </para>
    /// <para>
    /// Vector được nạp bởi <see cref="SemanticRouterWarmupService"/> chạy nền, nên khởi động ứng dụng
    /// không phụ thuộc vào nhà cung cấp embedding. Trước khi nạp xong — và trong mọi trường hợp lỗi —
    /// router trả <c>null</c>, tức là fail-open về đường RAG, giống cách LlmQueryNormalizer
    /// fail-open về câu hỏi gốc.
    /// </para>
    /// </summary>
    public sealed class EmbeddingSemanticRouter : ISemanticRouter
    {
        private readonly RouteCatalog _catalog;
        private readonly IRouteScorer _scorer;
        private readonly RouteUtteranceAdmin _utteranceAdmin;
        private readonly SemanticRouterConfig _config;
        private readonly ILogger<EmbeddingSemanticRouter> _logger;

        public EmbeddingSemanticRouter(RouteCatalog catalog,
                                       IRouteScorer scorer,
                                       RouteUtteranceAdmin utteranceAdmin,
                                       IOptions<SemanticRouterConfig> options,
                                       ILogger<EmbeddingSemanticRouter> logger)
        {
            _catalog = catalog;
            _scorer = scorer;
            _utteranceAdmin = utteranceAdmin;
            _config = options.Value;
            _logger = logger;
        }

        public RouteMatch? Route(string question, float[] questionEmbedding)
        {
            var routes = _catalog.Routes;

            if (routes is null || routes.Count == 0)
                return null;

            if (!IsRoutable(question, questionEmbedding))
                return null;

            var best = FindBest(routes, questionEmbedding);

            if (best is null)
            {
                _logger.LogDebug("Không route nào vượt ngưỡng cho câu \"{Question}\", dùng đường RAG.", question);
                return null;
            }

            _logger.LogDebug("Định tuyến khớp route {Route} (score={Score:F3}), bỏ qua truy hồi.",
                best.Route.Name, best.Score);

            return new RouteMatch(best.Route.Name, best.Score,
                best.Route.SystemPromptTemplate, best.Route.UserPromptTemplate);
        }

        public IReadOnlyList<RouteScore> Explain(string question, float[] questionEmbedding)
        {
            var routes = _catalog.Routes;

            if (routes is null || routes.Count == 0)
                return Array.Empty<RouteScore>();

            var routable = IsRoutable(question, questionEmbedding);

            return routes
                .Select(route =>
                {
                    var score = _scorer.Score(route.Vectors, questionEmbedding);
                    return new RouteScore(route.Name, score, route.Threshold,
                        Matched: routable && score >= route.Threshold);
                })
                .OrderByDescending(score => score.Score)
                .ToList();
        }

        public Task<RouteUpdateResult> AddUtterancesAsync(string routeName,
                                                          IReadOnlyList<string> utterances,
                                                          IReadOnlyList<float[]> vectors,
                                                          CancellationToken cancellationToken = default) =>
            _utteranceAdmin.AddUtterancesAsync(routeName, utterances, vectors, cancellationToken);

        private bool IsRoutable(string question, float[] questionEmbedding)
        {
            if (questionEmbedding.Length == 0)
                return false;

            // Câu dài gần như luôn là câu hỏi tri thức, kể cả khi mở đầu bằng một lời chào.
            if (question.Length > _config.MaxRoutableLength)
                return false;

            return true;
        }

        private BestMatch? FindBest(IReadOnlyList<RouteVectors> routes, float[] questionEmbedding)
        {
            BestMatch? best = null;

            foreach (var route in routes)
            {
                var score = _scorer.Score(route.Vectors, questionEmbedding);

                if (score < route.Threshold)
                    continue;

                if (best is null || score > best.Score)
                    best = new BestMatch(route, score);
            }

            return best;
        }

        private sealed record BestMatch(RouteVectors Route, double Score);
    }
}
