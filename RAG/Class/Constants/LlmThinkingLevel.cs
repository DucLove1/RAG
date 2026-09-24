namespace RAG.Class.Constants
{
    /// <summary>
    /// Mức suy nghĩ trung lập với provider, khai ở config của từng consumer. Mỗi provider tự dịch sang
    /// tham số của mình: Gemini → <c>thinking_level</c>, Groq → <c>reasoning_effort</c>.
    /// </summary>
    public enum LlmThinkingLevel
    {
        Minimal = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }
}
