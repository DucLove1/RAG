using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAG.Class;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Controllers
{
    /// <summary>
    /// Đường vận hành: chẩn đoán định tuyến, thêm câu mẫu lúc chạy, và số liệu cache.
    /// Không nằm trong luồng trả lời của người chơi.
    /// </summary>
    [Route("api/query")]
    [ApiController]
    public class RouteController : ControllerBase
    {
        private readonly IRouteDiagnostics _diagnostics;
        private readonly IRouteAdmin _routeAdmin;
        private readonly IQueryCacheStatistics _statistics;
        private readonly RouteMessagesConfig _messages;

        public RouteController(IRouteDiagnostics diagnostics,
                               IRouteAdmin routeAdmin,
                               IQueryCacheStatistics statistics,
                               IOptions<RouteMessagesConfig> messages)
        {
            _diagnostics = diagnostics;
            _routeAdmin = routeAdmin;
            _statistics = statistics;
            _messages = messages.Value;
        }

        /// <summary>
        /// Chẩn đoán định tuyến: trả về đánh giá của mọi route cho một câu hỏi.
        /// Không sinh câu trả lời và không chạm Qdrant.
        /// <para>
        /// Với <c>SemanticRouter:Strategy = Llm</c> thì endpoint này CÓ gọi LLM (một lượt phân
        /// loại), khác với bản trước vốn chỉ nhúng vector. Vẫn rẻ hơn nhiều so với chạy cả đường
        /// trả lời, và cố tình không đi qua cache định tuyến để kết quả luôn phản ánh prompt hiện tại.
        /// </para>
        /// </summary>
        [HttpPost("route-debug")]
        public async Task<IActionResult> PostRouteDebug([FromBody] RouteDebugRequest request,
                                                        CancellationToken cancellationToken = default)
        {
            var explanation = await _diagnostics.ExplainRouteAsync(request.Question, cancellationToken);

            return Ok(new RouteDebugResponse(
                request.Question,
                explanation.NormalizedQuestion,
                explanation.Strategy.ToString(),
                explanation.Match?.Name,
                explanation.Match?.Score,
                explanation.Scores));
        }

        /// <summary>
        /// Thêm câu mẫu vào một route đang chạy. Nhận câu dạng text (sẽ được nhúng),
        /// vector đã chuẩn bị sẵn, hoặc cả hai. Có hiệu lực ngay, không cần khởi động lại.
        /// </summary>
        [HttpPost("route-utterances")]
        public async Task<IActionResult> PostRouteUtterances([FromBody] AddUtterancesRequest request,
                                                             CancellationToken cancellationToken = default)
        {
            var utterances = request.Utterances ?? new List<string>();
            var vectors = request.Vectors ?? new List<float[]>();

            if (utterances.Count == 0 && vectors.Count == 0)
                return BadRequest();

            var result = await _routeAdmin.AddRouteUtterancesAsync(request.Route, utterances, vectors, cancellationToken);

            var response = new AddUtterancesResponse(
                result.Success,
                Describe(result),
                result.Added,
                result.Skipped,
                result.TotalInRoute,
                result.Persisted);

            // Không thêm được vì tên route sai hay dữ liệu không hợp lệ là lỗi của người gọi,
            // nên trả 400 thay vì 200 kèm success=false.
            return result.Success ? Ok(response) : BadRequest(response);
        }

        /// <summary>
        /// Tỉ lệ trúng cache. Không có số liệu này thì không cách nào biết cache đang thực sự
        /// tiết kiệm được gì hay chỉ đang chiếm RAM.
        /// </summary>
        [HttpGet("cache-stats")]
        public IActionResult GetCacheStats()
        {
            var stats = _statistics.GetStats();

            return Ok(new
            {
                normalization = new
                {
                    hits = stats.NormalizationHits,
                    misses = stats.NormalizationMisses,
                    hitRate = Math.Round(stats.NormalizationHitRate, 3)
                },
                embedding = new
                {
                    hits = stats.EmbeddingHits,
                    misses = stats.EmbeddingMisses,
                    hitRate = Math.Round(stats.EmbeddingHitRate, 3)
                },
                // Tầng đắt nhất khi trượt với chiến lược định tuyến bằng LLM: mỗi lần trượt là một
                // lượt gọi mô hình đầy đủ. Luôn 0 khi chạy chiến lược Embedding.
                route = new
                {
                    hits = stats.RouteHits,
                    misses = stats.RouteMisses,
                    hitRate = Math.Round(stats.RouteHitRate, 3)
                }
            });
        }

        /// <summary>
        /// Dịch mã trạng thái sang câu chữ. Đây là việc của tầng trình bày: cùng một kết quả
        /// nghiệp vụ có thể cần diễn đạt khác nhau tùy kênh, và câu chữ nằm ở configuration.
        /// </summary>
        private string Describe(RouteUpdateResult result) => result.Status switch
        {
            RouteUpdateStatus.Added => string.Format(
                result.Persisted ? _messages.Added : _messages.AddedNotPersisted,
                result.Added, result.RouteName, result.TotalInRoute),

            RouteUpdateStatus.UnknownRoute => string.Format(
                _messages.UnknownRoute,
                result.RouteName, string.Join(_messages.RouteNameSeparator, result.KnownRoutes)),

            RouteUpdateStatus.NotReady => _messages.NotReady,
            RouteUpdateStatus.NothingAdded => _messages.NothingAdded,
            RouteUpdateStatus.RouterDisabled => _messages.RouterDisabled,
            RouteUpdateStatus.NotSupported => _messages.NotSupported,
            _ => string.Empty
        };
    }
}
