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
        private readonly PromptConfig _promptConfig;

        public RAGPipline(ILLMProvider llmProvider,
                            IEmbeddingProvider embeddingProvider,
                            IQdrantProvider qdrantProvider,
                            IQueryNormalizer queryNormalizer,
                            IOptions<PromptConfig> promptConfig)
        {
            _llmProvider = llmProvider;
            _embeddingProvider = embeddingProvider;
            _qdrantProvider = qdrantProvider;
            _queryNormalizer = queryNormalizer;
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

            var dims = await _embeddingProvider.GetDimsAsync();

            await _qdrantProvider.EnsureCollectionExistsAsync((ulong)dims, cancellationToken);

            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(normalizedQuestion, cancellationToken);

            var filterByName = MatchPhrase(PayloadFields.NpcNames, npcName);
            var filter = new Filter();
            filter.Must.Add(filterByName);

            var resultVectors = await _qdrantProvider.SearchVectorsAsync(questionEmbedding, filter, topK, cancellationToken);

            // context sẽ là phần text của các vector kết quả, nối lại với nhau để làm ngữ cảnh cho LLM
            var context = string.Join(
                _promptConfig.ContextSeparator,
                resultVectors.Select(r => r.Payload[PayloadFields.Text]));

            var system = _promptConfig.BuildSystemPrompt(npcName, npcSystem);
            var user = _promptConfig.BuildUserPrompt(context, normalizedQuestion);

            return await _llmProvider.AskAsync(system, user, cancellationToken);
        }
    }
}
