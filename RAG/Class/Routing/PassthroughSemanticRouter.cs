using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Null Object cho <see cref="ISemanticRouter"/>: không route nào khớp, luôn đi đường RAG.
    /// Được đăng ký khi node định tuyến bị tắt hoặc không khai báo route hợp lệ nào,
    /// nhờ đó pipeline không cần biết đến cờ Enabled.
    /// </summary>
    public sealed class PassthroughSemanticRouter : ISemanticRouter
    {
        public RouteMatch? Route(string question, float[] questionEmbedding) => null;

        public IReadOnlyList<RouteScore> Explain(string question, float[] questionEmbedding) =>
            Array.Empty<RouteScore>();

        public Task<RouteUpdateResult> AddUtterancesAsync(string routeName,
                                                          IReadOnlyList<string> utterances,
                                                          IReadOnlyList<float[]> vectors,
                                                          CancellationToken cancellationToken = default) =>
            Task.FromResult(RouteUpdateResult.RouterDisabled());
    }
}
