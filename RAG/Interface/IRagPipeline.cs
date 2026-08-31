namespace RAG.Interface
{
    /// <summary>
    /// Sinh câu trả lời cho một câu hỏi của người chơi.
    /// </summary>
    public interface IAskService
    {
        /// <param name="topK">Số đoạn ngữ cảnh lấy về khi đi đường truy hồi.</param>
        Task<string> AskAsync(string npcName,
                              string npcSystem,
                              string question,
                              int topK,
                              CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Nạp tri thức vào kho vector.
    /// </summary>
    public interface IIngestionService
    {
        /// <summary>Tạo collection mới. Ném nếu collection đã tồn tại.</summary>
        Task CreateCollectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rút văn bản, cắt đoạn, nhúng và ghi vào kho vector; tạo collection nếu chưa có.
        /// </summary>
        Task<IngestionResult> IngestAsync(IReadOnlyList<DocumentSource> documents,
                                          string npcNames,
                                          CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Chẩn đoán định tuyến. Tách riêng vì đây là đường phục vụ vận hành, không nằm trong
    /// luồng trả lời — controller nào chỉ trả lời thì không nên nhìn thấy nó.
    /// </summary>
    public interface IRouteDiagnostics
    {
        /// <summary>
        /// Chuẩn hóa và nhúng câu hỏi rồi trả về điểm của mọi route.
        /// KHÔNG gọi LLM sinh câu trả lời và KHÔNG chạm kho vector, nên rất rẻ để tinh chỉnh ngưỡng.
        /// </summary>
        Task<RouteExplanation> ExplainRouteAsync(string question, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Quản trị câu mẫu của route lúc đang chạy.
    /// </summary>
    public interface IRouteAdmin
    {
        Task<RouteUpdateResult> AddRouteUtterancesAsync(string routeName,
                                                        IReadOnlyList<string> utterances,
                                                        IReadOnlyList<float[]> vectors,
                                                        CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Façade của toàn bộ stack RAG, gộp bốn vai trò trên.
    /// <para>
    /// Consumer nên phụ thuộc vào ĐÚNG vai trò mình cần chứ không phải interface gộp này (ISP):
    /// controller trả lời chỉ nhận <see cref="IAskService"/>, nhờ vậy nó không có cách nào gọi nhầm
    /// <see cref="IIngestionService.IngestAsync"/>. Interface gộp tồn tại để việc đăng ký DI và
    /// việc nhìn tổng thể luồng nằm ở một chỗ.
    /// </para>
    /// </summary>
    public interface IRagPipeline : IAskService, IIngestionService, IRouteDiagnostics, IRouteAdmin
    {
    }
}
