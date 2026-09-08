using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Answering
{
    /// <summary>
    /// Lõi của đường trả lời: chuẩn hóa → định tuyến → (điểm yếu → truy hồi) → sinh câu trả lời.
    /// <para>
    /// Bộ phát hiện điểm yếu CHỈ chạy trên nhánh nội dung game (không route nào khớp). Điều đó an
    /// toàn vì câu chốt bắt bài hung thủ luôn nói về vụ án, mà mọi route hiện có đều là những thứ
    /// KHÔNG phải nội dung game: chào hỏi, cảm ơn, tạm biệt, ngoài phạm vi. Thêm một route mang
    /// nội dung game vào <c>SemanticRouter:Routes</c> sẽ âm thầm che mất bộ phát hiện trên nhánh
    /// đó — kiểm bằng <c>route-debug</c> rằng câu chốt vẫn không khớp route nào.
    /// </para>
    /// </summary>
    public sealed class AskPipeline : IAskService
    {
        private readonly ILLMProvider _llmProvider;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IVectorStore _vectorStore;
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly ISemanticRouter _semanticRouter;
        private readonly IWeakPointDetector _weakPointDetector;
        private readonly ISemanticAnswerCache _answerCache;
        private readonly PromptConfig _promptConfig;

        public AskPipeline(ILLMProvider llmProvider,
                           IEmbeddingProvider embeddingProvider,
                           IVectorStore vectorStore,
                           IQueryNormalizer queryNormalizer,
                           ISemanticRouter semanticRouter,
                           IWeakPointDetector weakPointDetector,
                           ISemanticAnswerCache answerCache,
                           IOptions<PromptConfig> promptConfig)
        {
            _llmProvider = llmProvider;
            _embeddingProvider = embeddingProvider;
            _vectorStore = vectorStore;
            _queryNormalizer = queryNormalizer;
            _semanticRouter = semanticRouter;
            _weakPointDetector = weakPointDetector;
            _answerCache = answerCache;
            _promptConfig = promptConfig.Value;
        }

        public async Task<AskResult> AskAsync(string npcName,
                                              string npcSystem,
                                              string question,
                                              int topK,
                                              CancellationToken cancellationToken = default)
        {
            // Node chuẩn hóa: mở rộng từ viết tắt / sửa chính tả trước khi định tuyến và dựng prompt.
            var normalizedQuestion = await _queryNormalizer.NormalizeAsync(question, cancellationToken);

            // Node định tuyến chạy TRƯỚC bước nhúng: câu tán gẫu được trả lời thẳng và không tốn
            // lượt gọi API embedding nào. Bản trước nhúng trước rồi mới định tuyến, nên 100% câu
            // tán gẫu vẫn phải trả giá một lượt nhúng mà không dùng tới.
            // Không route nào khớp (null) thì mặc định đi đường truy hồi.
            var route = await _semanticRouter.RouteAsync(normalizedQuestion, cancellationToken);

            // Ngân sách token hỏi thẳng provider đang được inject (chính là provider chọn theo
            // LLM:Provider), nên con số trong prompt luôn đúng bằng con số API sẽ cắt.
            var lengthInstruction = _promptConfig.BuildLengthInstruction(_llmProvider.MaxOutputTokens);

            // Khớp route nghĩa là câu này KHÔNG hỏi nội dung game (chào hỏi, cảm ơn, tạm biệt, ngoài
            // phạm vi). Câu chốt bắt bài hung thủ thì luôn nói về vụ án, nên nó không bao giờ khớp
            // route — và chạy node điểm yếu ở đây chỉ là đốt một lượt gọi LLM để luôn nhận về
            // "không trúng". Vì vậy nhánh này thoát sớm, không đụng tới bộ phát hiện.
            if (route is not null)
            {
                var routedAnswer = await AnswerWithoutRetrievalAsync(
                    npcName, npcSystem, normalizedQuestion, route, lengthInstruction, cancellationToken);

                return new AskResult(routedAnswer, WeakPointHit: false);
            }

            // Từ đây trở xuống là đường nội dung game. Bộ phát hiện tự thoát ở phép tra từ điển khi
            // NPC không có mục trong WeakPoint:Targets, nên chi phí thực tế là một lượt gọi cho các
            // câu hỏi vụ án gửi tới ĐÚNG những NPC có điểm yếu.
            var weakPoint = await _weakPointDetector.DetectAsync(npcName, normalizedQuestion, cancellationToken);

            // Trúng thì KHÔNG truy hồi và KHÔNG gọi LLM trả lời: câu NPC nói lúc bị bắt bài là một
            // nhịp kịch bản, để LLM diễn đạt lại thì mỗi lần chơi ra một kiểu và mất tính xác định.
            if (weakPoint is not null)
                return new AskResult(weakPoint.Reply, WeakPointHit: true);

            var answer = await AnswerWithRetrievalAsync(
                npcName, npcSystem, normalizedQuestion, topK, lengthInstruction, cancellationToken);

            return new AskResult(answer, WeakPointHit: false);
        }

        /// <summary>
        /// Nhánh tán gẫu: vẫn để LLM sinh câu trả lời theo đúng persona của NPC,
        /// chỉ khác là không kèm ngữ cảnh truy hồi nào nên không chạm tới kho vector.
        /// </summary>
        private Task<string> AnswerWithoutRetrievalAsync(string npcName,
                                                         string npcSystem,
                                                         string question,
                                                         RouteMatch route,
                                                         string lengthInstruction,
                                                         CancellationToken cancellationToken) =>
            _llmProvider.AskAsync(
                route.BuildSystemPrompt(npcName, npcSystem, lengthInstruction),
                route.BuildUserPrompt(question),
                cancellationToken: cancellationToken);

        /// <summary>
        /// Nhánh RAG mặc định: nhúng câu hỏi, tìm ngữ cảnh trong kho vector rồi mới sinh câu trả lời.
        /// <para>
        /// Việc nhúng nằm ở đây chứ không ở đầu pipeline vì chỉ nhánh này mới cần tới vector.
        /// Chiến lược định tuyến bằng embedding cũng nhúng câu hỏi, nhưng nó đi qua decorator cache
        /// nên lần nhúng ở đây là một lần trúng cache — tổng vẫn đúng một lượt gọi API mỗi request.
        /// </para>
        /// </summary>
        private async Task<string> AnswerWithRetrievalAsync(string npcName,
                                                            string npcSystem,
                                                            string question,
                                                            int topK,
                                                            string lengthInstruction,
                                                            CancellationToken cancellationToken)
        {
            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(question, cancellationToken);

            // Cache ngữ nghĩa nằm ĐÚNG SAU bước nhúng và TRƯỚC Qdrant: vector vừa có ở dòng trên,
            // nên một lần trúng cache cắt bỏ đúng hai thứ đắt nhất còn lại của request — truy hồi
            // và lượt gọi LLM — mà không phát sinh thêm lời gọi API nào.
            //
            // Đặt ở đầu AskAsync thì tiết kiệm thêm được cả bước nhúng, nhưng lúc đó chưa có
            // vector, và quan trọng hơn là nó sẽ nuốt luôn nhánh định tuyến lẫn nhánh điểm yếu.
            // Nhánh điểm yếu là chỗ chết người: cache khớp GẦN ĐÚNG, nên một câu chỉ hao hao câu
            // chốt sẽ kích hoạt nhịp bắt bài cho người chơi chưa hề suy luận ra — mà AskResult trả
            // về từ cache lại mang WeakPointHit = false, nên game cũng không ghi nhận đó là sự
            // kiện cốt truyện. Nó còn đóng băng cả câu tán gẫu, vốn phải đổi giọng mỗi lần gặp.
            var cacheQuery = new SemanticAnswerQuery(npcName, npcSystem, question, questionEmbedding);

            var cached = await _answerCache.TryGetAsync(cacheQuery, cancellationToken);
            if (cached is not null)
                return cached.Answer;

            // Không gọi EnsureCollectionExistsAsync ở đây: đường trả lời chỉ ĐỌC, và collection đã
            // được đảm bảo ở đường nạp dữ liệu. Bản trước gọi ở mỗi request, tốn một round-trip
            // gRPC cho 100% traffic mà không lần nào làm gì khác ngoài xác nhận điều đã biết.
            var filter = VectorSearchFilter.Match(PayloadFields.NpcNames, npcName);

            var hits = await _vectorStore.SearchAsync(questionEmbedding, filter, topK, cancellationToken);

            // Ngữ cảnh là phần text của các kết quả, nối lại với nhau để đưa vào prompt.
            var context = string.Join(
                _promptConfig.ContextSeparator,
                hits.Select(hit => hit.Payload[PayloadFields.Text]));

            var answer = await _llmProvider.AskAsync(
                _promptConfig.BuildSystemPrompt(npcName, npcSystem, lengthInstruction),
                _promptConfig.BuildUserPrompt(context, question),
                cancellationToken: cancellationToken);

            // Cờ hasContext để cache tự quyết định có ghi hay không: caller biết truy hồi có ra gì
            // không, cache thì không. Cùng kiểu chia việc với tham số unchanged của
            // INormalizationCache. Câu trả lời dựng trên ngữ cảnh rỗng gần như luôn là "tôi không
            // biết", ghi lại là đóng băng một lần Qdrant hụt thành câu trả lời chính thức.
            await _answerCache.SetAsync(cacheQuery, answer, hasContext: hits.Count > 0, cancellationToken);

            return answer;
        }
    }
}
