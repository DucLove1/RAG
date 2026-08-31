namespace RAG.Class.Constants
{
    /// <summary>
    /// Khóa định danh cho các pool API key khi đăng ký <see cref="Interface.IApiKeyRotator"/> dùng Keyed Services.
    /// Tương tự <see cref="LlmProviderKey"/> để tránh magic string.
    /// </summary>
    public enum ApiKeyPoolKey
    {
        Groq = 0,
        GeminiLlm = 1,
        GeminiEmbedding = 2
    }
}
