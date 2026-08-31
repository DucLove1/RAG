namespace RAG.Interface
{
    /// <summary>
    /// Mọi API key trong pool đều bị giới hạn tần suất (429). Không còn key nào để thử.
    /// </summary>
    public sealed class AllApiKeysRateLimitedException : Exception
    {
        public AllApiKeysRateLimitedException(string message) : base(message) { }
    }
}
