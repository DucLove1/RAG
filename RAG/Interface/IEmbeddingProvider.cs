namespace RAG.Interface
{
    public interface IEmbeddingProvider
    {
        Task<float[]> GetEmbeddingsAsync(string input, CancellationToken cancellationToken = default);
        Task<int> GetDimsAsync();
    }
}
