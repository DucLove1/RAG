using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Null Object cho <see cref="ISemanticRouter"/>: không route nào khớp, luôn đi đường RAG.
    /// Được đăng ký khi <c>SemanticRouter:Strategy = Off</c> hoặc không khai báo route hợp lệ nào,
    /// nhờ đó pipeline không cần biết đến chiến lược đang chạy.
    /// </summary>
    public sealed class PassthroughSemanticRouter : ISemanticRouter, IRouteExplainer
    {
        public Task<RouteMatch?> RouteAsync(string question, CancellationToken cancellationToken = default) =>
            Task.FromResult<RouteMatch?>(null);

        public Task<RouteExplanation> ExplainAsync(string normalizedQuestion,
                                                   CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteExplanation(normalizedQuestion, Array.Empty<RouteScore>(), null,
                SemanticRouterStrategy.Off));
    }
}
