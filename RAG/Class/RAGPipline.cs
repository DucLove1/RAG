using Qdrant.Client.Grpc;
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

        public RAGPipline(ILLMProvider llmProvider, 
                            IEmbeddingProvider embeddingProvider, 
                            IQdrantProvider qdrantProvider)
        {
            _llmProvider = llmProvider;
            _embeddingProvider = embeddingProvider;
            _qdrantProvider = qdrantProvider;
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
                        { "npcNames", npcNames },
                        { "text", text },
                        { "source", source ?? string.Empty }
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
            var dims = await _embeddingProvider.GetDimsAsync();

            await _qdrantProvider.EnsureCollectionExistsAsync((ulong)dims, cancellationToken);

            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(question, cancellationToken);

            var filterByName = MatchPhrase("npcNames", npcName);
            var filter = new Filter();
            filter.Must.Add(filterByName);

            var resultVectors = await _qdrantProvider.SearchVectorsAsync(questionEmbedding, filter, topK, cancellationToken);

            // context sẽ là phần text của các vector kết quả, nối lại với nhau để làm ngữ cảnh cho LLM
            var context = string.Join("\n", resultVectors.Select(r => r.Payload["text"]));

            // Tạo prompt cho LLM, có thể tùy chỉnh thêm để hướng dẫn LLM trả lời tốt hơn
            var system = $"Bạn là {npcName}, " +
                $"bạn có tính cách {npcSystem}, " +
                "giúp trả lời các câu hỏi dựa trên ngữ cảnh được cung cấp. " +
                "Nếu không có thì cứ trả lời không biết, giữ câu trả lời thật trung lập";

            // User prompt sẽ bao gồm câu hỏi và ngữ cảnh, cách trình bày có thể tùy chỉnh để LLM hiểu rõ hơn
            var user = $"{context} \n\n {question}\n";
            var answer = await _llmProvider.AskAsync(system, user, cancellationToken);
            return answer;
        }
    }
}
