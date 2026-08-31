using RAG.Interface;

namespace RAG.Class
{
    /// <summary>
    /// Façade của toàn bộ stack RAG: một chỗ duy nhất nhìn thấy cả bốn vai trò.
    /// <para>
    /// Mỗi method chỉ ủy quyền một dòng. Chủ ý là vậy: nơi nào cần đúng một vai trò thì nên nhận
    /// đúng interface vai trò đó (ISP) — controller trả lời nhận <see cref="IAskService"/>, không
    /// nhận cả façade. Façade tồn tại để có một điểm nhìn tổng thể luồng, và để nơi nào thực sự
    /// cần nhiều vai trò không phải nhận bốn tham số.
    /// </para>
    /// </summary>
    public sealed class RagPipeline : IRagPipeline
    {
        private readonly IAskService _askService;
        private readonly IIngestionService _ingestionService;
        private readonly IRouteDiagnostics _routeDiagnostics;
        private readonly IRouteUtteranceAdmin _routeUtteranceAdmin;

        public RagPipeline(IAskService askService,
                           IIngestionService ingestionService,
                           IRouteDiagnostics routeDiagnostics,
                           IRouteUtteranceAdmin routeUtteranceAdmin)
        {
            _askService = askService;
            _ingestionService = ingestionService;
            _routeDiagnostics = routeDiagnostics;
            _routeUtteranceAdmin = routeUtteranceAdmin;
        }

        public Task<string> AskAsync(string npcName,
                                     string npcSystem,
                                     string question,
                                     int topK,
                                     CancellationToken cancellationToken = default) =>
            _askService.AskAsync(npcName, npcSystem, question, topK, cancellationToken);

        public Task CreateCollectionAsync(CancellationToken cancellationToken = default) =>
            _ingestionService.CreateCollectionAsync(cancellationToken);

        public Task<IngestionResult> IngestAsync(IReadOnlyList<DocumentSource> documents,
                                                 string npcNames,
                                                 CancellationToken cancellationToken = default) =>
            _ingestionService.IngestAsync(documents, npcNames, cancellationToken);

        public Task<RouteExplanation> ExplainRouteAsync(string question,
                                                        CancellationToken cancellationToken = default) =>
            _routeDiagnostics.ExplainRouteAsync(question, cancellationToken);

        public Task<RouteUpdateResult> AddRouteUtterancesAsync(string routeName,
                                                               IReadOnlyList<string> utterances,
                                                               IReadOnlyList<float[]> vectors,
                                                               CancellationToken cancellationToken = default) =>
            _routeUtteranceAdmin.AddUtterancesAsync(routeName, utterances, vectors, cancellationToken);
    }
}
