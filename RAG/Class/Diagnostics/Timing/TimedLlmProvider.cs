using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo lượt gọi LLM SINH CÂU TRẢ LỜI.
    /// <para>
    /// CHỈ bọc đăng ký <see cref="ILLMProvider"/> KHÔNG KHÓA — cái mà <c>AskPipeline</c> nhận. Các
    /// provider keyed (<c>LlmProviderKey.Groq</c> / <c>.Gemini</c>) mà bộ chuẩn hóa, bộ định tuyến
    /// và bộ phát hiện điểm yếu lấy qua <c>ILlmProviderResolver</c> thì CỐ TÌNH không bọc: bọc cả
    /// hai chỗ thì lượt gọi của bộ chuẩn hóa bị tính hai lần — một lần dưới stage <c>normalize</c>,
    /// một lần nữa dưới <c>llmAnswer</c> — và tổng các stage sẽ vượt quá cả thời gian thật của
    /// request. Thời gian của chúng đã nằm trong stage riêng của chính chúng.
    /// </para>
    /// </summary>
    public sealed class TimedLlmProvider : ILLMProvider
    {
        private readonly ILLMProvider _inner;
        private readonly ILatencyTracker _latency;

        public TimedLlmProvider(ILLMProvider inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public int MaxOutputTokens => _inner.MaxOutputTokens;

        public Task<string> AskAsync(string system,
                                     string user,
                                     string? model = null,
                                     CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(LatencyStages.LlmAnswer, () => _inner.AskAsync(system, user, model, cancellationToken));
    }
}
