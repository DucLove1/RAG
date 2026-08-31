using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Answering
{
    /// <summary>
    /// Lõi của đường trả lời: chuẩn hóa → nhúng → định tuyến → (truy hồi) → sinh câu trả lời.
    /// </summary>
    public sealed class AskPipeline : IAskService
    {
        private readonly ILLMProvider _llmProvider;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IVectorStore _vectorStore;
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly ISemanticRouter _semanticRouter;
        private readonly PromptConfig _promptConfig;

        public AskPipeline(ILLMProvider llmProvider,
                           IEmbeddingProvider embeddingProvider,
                           IVectorStore vectorStore,
                           IQueryNormalizer queryNormalizer,
                           ISemanticRouter semanticRouter,
                           IOptions<PromptConfig> promptConfig)
        {
            _llmProvider = llmProvider;
            _embeddingProvider = embeddingProvider;
            _vectorStore = vectorStore;
            _queryNormalizer = queryNormalizer;
            _semanticRouter = semanticRouter;
            _promptConfig = promptConfig.Value;
        }

        public async Task<string> AskAsync(string npcName,
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

            return route is not null
                ? await AnswerWithoutRetrievalAsync(npcName, npcSystem, normalizedQuestion, route, cancellationToken)
                : await AnswerWithRetrievalAsync(npcName, npcSystem, normalizedQuestion, topK, cancellationToken);
        }

        /// <summary>
        /// Nhánh tán gẫu: vẫn để LLM sinh câu trả lời theo đúng persona của NPC,
        /// chỉ khác là không kèm ngữ cảnh truy hồi nào nên không chạm tới kho vector.
        /// </summary>
        private Task<string> AnswerWithoutRetrievalAsync(string npcName,
                                                         string npcSystem,
                                                         string question,
                                                         RouteMatch route,
                                                         CancellationToken cancellationToken) =>
            _llmProvider.AskAsync(
                route.BuildSystemPrompt(npcName, npcSystem),
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
                                                            CancellationToken cancellationToken)
        {
            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(question, cancellationToken);

            // Không gọi EnsureCollectionExistsAsync ở đây: đường trả lời chỉ ĐỌC, và collection đã
            // được đảm bảo ở đường nạp dữ liệu. Bản trước gọi ở mỗi request, tốn một round-trip
            // gRPC cho 100% traffic mà không lần nào làm gì khác ngoài xác nhận điều đã biết.
            var filter = VectorSearchFilter.Match(PayloadFields.NpcNames, npcName);

            var hits = await _vectorStore.SearchAsync(questionEmbedding, filter, topK, cancellationToken);

            // Ngữ cảnh là phần text của các kết quả, nối lại với nhau để đưa vào prompt.
            var context = string.Join(
                _promptConfig.ContextSeparator,
                hits.Select(hit => hit.Payload[PayloadFields.Text]));

            return await _llmProvider.AskAsync(
                _promptConfig.BuildSystemPrompt(npcName, npcSystem),
                _promptConfig.BuildUserPrompt(context, question),
                cancellationToken: cancellationToken);
        }
    }
}
