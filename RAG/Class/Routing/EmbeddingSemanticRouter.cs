using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
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
    /// Tự nhúng câu hỏi thay vì nhận vector từ caller: hợp đồng <see cref="ISemanticRouter"/> phải
    /// phục vụ được cả chiến lược không cần vector nào. Việc này KHÔNG làm tăng số lượt gọi API,
    /// vì <see cref="IEmbeddingProvider"/> tiêm vào đây đã bị bọc bởi decorator cache — lần nhúng
    /// sau ở nhánh truy hồi của pipeline là một lần trúng cache.
    /// <br/>
    /// Đánh đổi: điều đó chỉ đúng khi <c>QueryCache:Enabled = true</c>. Tắt cache mà vẫn chạy
    /// chiến lược này thì mỗi request RAG tốn HAI lượt nhúng; module đăng ký sẽ log cảnh báo lúc
    /// khởi động nếu gặp đúng tổ hợp đó.
    /// </para>
    /// <para>
    /// Vector câu mẫu được nạp bởi <see cref="SemanticRouterWarmupService"/> chạy nền, nên khởi
    /// động ứng dụng không phụ thuộc vào nhà cung cấp embedding. Trước khi nạp xong — và trong mọi
    /// trường hợp lỗi — router trả <c>null</c>, tức là fail-open về đường RAG.
    /// </para>
    /// </summary>
    public sealed class EmbeddingSemanticRouter : ISemanticRouter, IRouteExplainer
    {
        private readonly RouteCatalog _catalog;
        private readonly IRouteScorer _scorer;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly EmbeddingRouterConfig _config;
        private readonly ILogger<EmbeddingSemanticRouter> _logger;

        public EmbeddingSemanticRouter(RouteCatalog catalog,
                                       IRouteScorer scorer,
                                       IEmbeddingProvider embeddingProvider,
                                       IOptions<SemanticRouterConfig> options,
                                       ILogger<EmbeddingSemanticRouter> logger)
        {
            _catalog = catalog;
            _scorer = scorer;
            _embeddingProvider = embeddingProvider;
            _config = options.Value.Embedding;
            _logger = logger;
        }

        public async Task<RouteMatch?> RouteAsync(string question, CancellationToken cancellationToken = default)
        {
            var routes = _catalog.Routes;

            if (routes is null || routes.Count == 0)
                return null;

            if (!IsRoutable(question))
                return null;

            var questionEmbedding = await EmbedAsync(question, cancellationToken);

            if (questionEmbedding.Length == 0)
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

        public async Task<RouteExplanation> ExplainAsync(string normalizedQuestion,
                                                         CancellationToken cancellationToken = default)
        {
            var routes = _catalog.Routes;

            if (routes is null || routes.Count == 0)
            {
                return new RouteExplanation(normalizedQuestion, Array.Empty<RouteScore>(), null,
                    SemanticRouterStrategy.Embedding);
            }

            var routable = IsRoutable(normalizedQuestion);
            var questionEmbedding = await EmbedAsync(normalizedQuestion, cancellationToken);

            if (questionEmbedding.Length == 0)
            {
                return new RouteExplanation(normalizedQuestion, Array.Empty<RouteScore>(), null,
                    SemanticRouterStrategy.Embedding);
            }

            var scores = routes
                .Select(route =>
                {
                    var score = _scorer.Score(route.Vectors, questionEmbedding);
                    return new RouteScore(route.Name, score, route.Threshold,
                        Matched: routable && score >= route.Threshold);
                })
                .OrderByDescending(score => score.Score)
                .ToList();

            // Chấm điểm một lần rồi tự suy ra route thắng, thay vì gọi lại đường định tuyến —
            // vừa tránh một lần nhúng nữa, vừa đảm bảo điểm và kết luận luôn khớp nhau.
            var winner = routable ? FindBest(routes, questionEmbedding) : null;

            var match = winner is null
                ? null
                : new RouteMatch(winner.Route.Name, winner.Score,
                    winner.Route.SystemPromptTemplate, winner.Route.UserPromptTemplate);

            return new RouteExplanation(normalizedQuestion, scores, match, SemanticRouterStrategy.Embedding);
        }

        /// <summary>
        /// Nhúng câu hỏi, fail-open về mảng rỗng. Nhà cung cấp embedding NÉM khi lỗi, và lỗi đó
        /// không được phép làm hỏng cả request: nhánh truy hồi ngay sau đó sẽ nhúng lại và ném
        /// tiếp, lúc ấy bộ xử lý lỗi mới dịch nó thành 503 đúng chỗ.
        /// </summary>
        private async Task<float[]> EmbedAsync(string question, CancellationToken cancellationToken)
        {
            try
            {
                return await _embeddingProvider.GetEmbeddingsAsync(question, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nhúng câu hỏi để định tuyến thất bại, dùng đường RAG.");
                return Array.Empty<float>();
            }
        }

        private bool IsRoutable(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return false;

            // Câu dài gần như luôn là câu hỏi tri thức, kể cả khi mở đầu bằng một lời chào.
            // Kiểm tra TRƯỚC khi nhúng để câu dài không tốn lượt gọi API nào.
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
