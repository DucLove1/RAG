using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Null Object cho <see cref="IRouteUtteranceAdmin"/> khi node định tuyến đang tắt
    /// (<c>SemanticRouter:Strategy = Off</c>): không có route nào để thêm câu mẫu vào.
    /// </summary>
    public sealed class DisabledRouteUtteranceAdmin : IRouteUtteranceAdmin
    {
        public Task<RouteUpdateResult> AddUtterancesAsync(string routeName,
                                                          IReadOnlyList<string> utterances,
                                                          IReadOnlyList<float[]> vectors,
                                                          CancellationToken cancellationToken = default) =>
            Task.FromResult(RouteUpdateResult.RouterDisabled());
    }
}
