namespace RAG.Class.Constants
{
    /// <summary>
    /// Khóa định danh cho các <see cref="Interface.ILLMProvider"/> được đăng ký trùng interface.
    /// Dùng enum thay cho magic string để tránh sai chính tả và bind được trực tiếp từ configuration.
    /// </summary>
    public enum LlmProviderKey
    {
        Groq = 0,
        Gemini = 1
    }
}
