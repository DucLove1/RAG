using RAG.Class.Constants;

namespace RAG.Class.Dto
{
    /// <summary>
    /// Tuỳ chọn theo từng lời gọi LLM. <see cref="Model"/> trống thì dùng model mặc định của provider;
    /// <see cref="ThinkingLevel"/> trống thì KHÔNG gửi, để model chạy theo mặc định của nhà cung cấp.
    /// </summary>
    public sealed record LlmRequestOptions(string? Model = null, LlmThinkingLevel? ThinkingLevel = null);
}
