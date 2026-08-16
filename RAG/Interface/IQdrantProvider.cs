using Qdrant.Client.Grpc;
using static RAG.Class.QdrantProvider;

namespace RAG.Interface
{
    public interface IQdrantProvider
    {
        Task CreateCollectionAsync(CancellationToken cancellationToken = default);
        Task EnsureCollectionExistsAsync(ulong dimension, CancellationToken cancellationToken = default);
        Task UpsertVectorAsync(IEnumerable<QdrantPointInput> pointsData, CancellationToken cancellationToken = default);
        Task<List<ScoredPointResult>> SearchVectorsAsync(float[] queryVector, Filter? filter = null, int limit = 5, CancellationToken cancellationToken = default);
    }
    public record ScoredPointResult(Guid Id, float Score, Dictionary<string, string> Payload);
}
