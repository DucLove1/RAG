namespace RAG.Interface
{
    public interface ILLMProvider
    {
        Task<string> AskAsync(string system, string user, CancellationToken cancellationToken=default);
    }
}
