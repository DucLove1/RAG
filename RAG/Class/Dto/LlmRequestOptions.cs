using RAG.Class.Constants;

namespace RAG.Class.Dto
{
    /// <summary>
    /// Tuỳ chọn theo từng lời gọi LLM. <see cref="Model"/> trống thì dùng model mặc định của provider;
    /// <see cref="ThinkingLevel"/> trống thì KHÔNG gửi, để model chạy theo mặc định của nhà cung cấp.
    /// <see cref="MaxOutputTokens"/> trống thì dùng trần token của section provider — trần đó được chỉnh
    /// cho câu thoại NPC ngắn, quá chật cho việc như tóm tắt hội thoại.
    /// </summary>
    public sealed record LlmRequestOptions(string? Model = null,
                                           LlmThinkingLevel? ThinkingLevel = null,
                                           int? MaxOutputTokens = null);
}
