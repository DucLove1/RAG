using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using static RAG.Class.QdrantProvider;
using static Qdrant.Client.Grpc.Conditions;

namespace RAG.Class
{
    public class RAGPipline
    {
        private readonly ILLMProvider _llmProvider;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IQdrantProvider _qdrantProvider;
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly ISemanticRouter _semanticRouter;
        private readonly PromptConfig _promptConfig;

        public RAGPipline(ILLMProvider llmProvider,
                            IEmbeddingProvider embeddingProvider,
                            IQdrantProvider qdrantProvider,
                            IQueryNormalizer queryNormalizer,
                            ISemanticRouter semanticRouter,
                            IOptions<PromptConfig> promptConfig)
        {
            _llmProvider = llmProvider;
            _embeddingProvider = embeddingProvider;
            _qdrantProvider = qdrantProvider;
            _queryNormalizer = queryNormalizer;
            _semanticRouter = semanticRouter;
            _promptConfig = promptConfig.Value;
        }

        public async Task CreateCollection(CancellationToken cancellationToken = default)
        {
            await _qdrantProvider.CreateCollectionAsync(cancellationToken);
        }

        public async Task IngestAsync(IEnumerable<(string npcNames, string text, string? source)> chunks, CancellationToken cancellationToken = default)
        {
            int dim = await _embeddingProvider.GetDimsAsync();
            await _qdrantProvider.EnsureCollectionExistsAsync((ulong)dim, cancellationToken);

            var pointsData = new List<QdrantPointInput>();
            foreach (var (npcNames, text, source) in chunks)
            {
                var embedding = await _embeddingProvider.GetEmbeddingsAsync(text, cancellationToken);
                var point = new QdrantPointInput
                (
                    Guid.NewGuid(),
                    embedding,
                    new Dictionary<string, object>
                    {
                        { PayloadFields.NpcNames, npcNames },
                        { PayloadFields.Text, text },
                        { PayloadFields.Source, source ?? string.Empty }
                    }
                );
                pointsData.Add(point);
            }

            await _qdrantProvider.UpsertVectorAsync(pointsData, cancellationToken);
        }

        public async Task<string> AskAsync(string npcName,
                                           string npcSystem,
                                           string question,
                                           int topK,
                                           CancellationToken cancellationToken = default)
        {
            // Node chuẩn hóa: mở rộng từ viết tắt / sửa chính tả trước khi embedding và dựng prompt.
            var normalizedQuestion = await _queryNormalizer.NormalizeAsync(question, cancellationToken);

            // Vector câu hỏi được tính đúng một lần, dùng chung cho cả định tuyến lẫn truy hồi.
            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(normalizedQuestion, cancellationToken);

            // Node định tuyến: câu tán gẫu được trả lời thẳng, bỏ qua hoàn toàn Qdrant.
            // Không route nào khớp (null) thì mặc định đi đường truy hồi.
            var route = _semanticRouter.Route(normalizedQuestion, questionEmbedding);

            return route is not null
                ? await AnswerWithoutRetrievalAsync(npcName, npcSystem, normalizedQuestion, route, cancellationToken)
                : await AnswerWithRetrievalAsync(npcName, npcSystem, normalizedQuestion, questionEmbedding, topK, cancellationToken);
        }

        /// <summary>
        /// Chẩn đoán định tuyến: chuẩn hóa và embedding câu hỏi rồi trả về điểm của mọi route.
        /// KHÔNG gọi LLM và KHÔNG chạm Qdrant, nên dùng để tinh chỉnh ngưỡng rất rẻ.
        /// </summary>
        public async Task<(string NormalizedQuestion, IReadOnlyList<RouteScore> Scores, RouteMatch? Match)> ExplainRouteAsync(
            string question, CancellationToken cancellationToken = default)
        {
            var normalizedQuestion = await _queryNormalizer.NormalizeAsync(question, cancellationToken);
            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(normalizedQuestion, cancellationToken);

            var scores = _semanticRouter.Explain(normalizedQuestion, questionEmbedding);
            var match = _semanticRouter.Route(normalizedQuestion, questionEmbedding);

            return (normalizedQuestion, scores, match);
        }

        /// <summary>
        /// Thêm câu mẫu vào một route đang chạy, không cần khởi động lại ứng dụng.
        /// </summary>
        public Task<RouteUpdateResult> AddRouteUtterancesAsync(string routeName,
                                                               IReadOnlyList<string> utterances,
                                                               IReadOnlyList<float[]> vectors,
                                                               CancellationToken cancellationToken = default) =>
            _semanticRouter.AddUtterancesAsync(routeName, utterances, vectors, cancellationToken);

        /// <summary>
        /// Nhánh tán gẫu: vẫn để LLM sinh câu trả lời theo đúng persona của NPC,
        /// chỉ khác là không kèm ngữ cảnh truy hồi nào nên không chạm tới Qdrant.
        /// </summary>
        private async Task<string> AnswerWithoutRetrievalAsync(string npcName,
                                                               string npcSystem,
                                                               string question,
                                                               RouteMatch route,
                                                               CancellationToken cancellationToken)
        {
            var system = route.BuildSystemPrompt(npcName, npcSystem);
            var user = route.BuildUserPrompt(question);

            return await _llmProvider.AskAsync(system, user, cancellationToken);
        }

        /// <summary>
        /// Nhánh RAG mặc định: tìm ngữ cảnh trong Qdrant rồi mới sinh câu trả lời.
        /// </summary>
        private async Task<string> AnswerWithRetrievalAsync(string npcName,
                                                            string npcSystem,
                                                            string question,
                                                            float[] questionEmbedding,
                                                            int topK,
                                                            CancellationToken cancellationToken)
        {
            var dims = await _embeddingProvider.GetDimsAsync();

            await _qdrantProvider.EnsureCollectionExistsAsync((ulong)dims, cancellationToken);

            var filterByName = MatchPhrase(PayloadFields.NpcNames, npcName);
            var filter = new Filter();
            filter.Must.Add(filterByName);

            var resultVectors = await _qdrantProvider.SearchVectorsAsync(questionEmbedding, filter, topK, cancellationToken);

            // context sẽ là phần text của các vector kết quả, nối lại với nhau để làm ngữ cảnh cho LLM
            var context = string.Join(
                _promptConfig.ContextSeparator,
                resultVectors.Select(r => r.Payload[PayloadFields.Text]));

            var system = _promptConfig.BuildSystemPrompt(npcName, npcSystem);
            var user = _promptConfig.BuildUserPrompt(context, question);

            return await _llmProvider.AskAsync(system, user, cancellationToken);
        }
    }
}
