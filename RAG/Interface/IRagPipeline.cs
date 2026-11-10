namespace RAG.Interface
{
    /// <summary>
    /// Sinh câu trả lời cho một câu hỏi của người chơi.
    /// </summary>
    public interface IAskService
    {
        /// <param name="topK">Số đoạn ngữ cảnh lấy về khi đi đường truy hồi.</param>
        /// <returns>
        /// Câu trả lời kèm cờ <c>WeakPointHit</c> (luôn false, giữ lại để client cũ không vỡ).
        /// </returns>
        /// <param name="history">Lịch sử hội thoại client gửi kèm; <see cref="ConversationHistory.Empty"/> khi không có.</param>
        /// <param name="stage">
        /// Giai đoạn mạch chuyện hiện tại, do client gửi lên; <c>null</c> thì không lọc theo stage
        /// — hành vi y hệt trước khi có tính năng này. Xem <c>RequestDto.Stage</c>.
        /// </param>
        Task<AskResult> AskAsync(string npcName,
                                 string npcSystem,
                                 string question,
                                 ConversationHistory history,
                                 int topK,
                                 string? stage = null,
                                 CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Sinh câu trả lời theo LUỒNG, từng mảnh một.
    /// <para>
    /// Tách khỏi <see cref="IAskService"/> chứ không thêm method vào đó (ISP): mọi consumer hiện có
    /// đều cần câu trả lời NGUYÊN KHỐI, nên nhét method streaming vào đó là bắt mọi cài đặt tương
    /// lai phải viết một thứ không ai ở đó gọi tới.
    /// </para>
    /// <para>
    /// BẤT BIẾN của chuỗi sự kiện: đúng MỘT <see cref="AskStreamMetaEvent"/> luôn đi TRƯỚC mọi
    /// <see cref="AskStreamTokenEvent"/>. Sự kiện này được phát sau bước nhúng chứ không sớm hơn, để
    /// lỗi nhúng vẫn còn biến được thành mã HTTP đúng thay vì một sự kiện lỗi trong response 200.
    /// </para>
    /// <para>
    /// KHÔNG sinh sự kiện <see cref="AskStreamDoneEvent"/> hay <see cref="AskStreamErrorEvent"/>:
    /// hai cái đó là khung TRUYỀN, do tầng ghi ra dây sinh. "Hết" ở đây đơn giản là luồng cạn.
    /// </para>
    /// </summary>
    public interface IAskStreamService
    {
        IAsyncEnumerable<AskStreamEvent> AskStreamAsync(string npcName,
                                                        string npcSystem,
                                                        string question,
                                                        ConversationHistory history,
                                                        int topK,
                                                        string? stage = null,
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
        /// Chuẩn hóa câu hỏi rồi trả về đánh giá của mọi route.
        /// KHÔNG sinh câu trả lời và KHÔNG chạm kho vector.
        /// <para>
        /// Chi phí phụ thuộc chiến lược đang chạy: với <c>Embedding</c> đây là một lần nhúng, rẻ
        /// và lặp lại thoải mái; với <c>Llm</c> đây là một lượt gọi mô hình đầy đủ — vẫn rẻ hơn
        /// nhiều so với chạy cả đường trả lời, nhưng không còn miễn phí như trước.
        /// </para>
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
    /// Façade của toàn bộ stack RAG, gộp năm vai trò trên.
    /// <para>
    /// Consumer nên phụ thuộc vào ĐÚNG vai trò mình cần chứ không phải interface gộp này (ISP):
    /// controller trả lời chỉ nhận <see cref="IAskService"/>, nhờ vậy nó không có cách nào gọi nhầm
    /// <see cref="IIngestionService.IngestAsync"/>. Interface gộp tồn tại để việc đăng ký DI và
    /// việc nhìn tổng thể luồng nằm ở một chỗ.
    /// </para>
    /// </summary>
    public interface IRagPipeline : IAskService, IAskStreamService, IIngestionService, IRouteDiagnostics, IRouteAdmin
    {
    }
}
