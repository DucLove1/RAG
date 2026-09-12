using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo bước chuẩn hóa câu hỏi.
    /// <para>
    /// Bọc NGOÀI <c>CachingQueryNormalizer</c>, nên số đo là chi phí thật mà pipeline phải trả: một
    /// lần trúng cache hiện ra dưới dạng vài chục micro-giây, một lần trượt hiện ra dưới dạng cả
    /// lượt gọi LLM. Bọc phía trong cache thì các lần trúng biến mất khỏi log và con số trung bình
    /// sẽ nói dối theo hướng tệ hơn thực tế.
    /// </para>
    /// </summary>
    public sealed class TimedQueryNormalizer : IQueryNormalizer
    {
        private readonly IQueryNormalizer _inner;
        private readonly ILatencyTracker _latency;

        public TimedQueryNormalizer(IQueryNormalizer inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public Task<string> NormalizeAsync(string question, CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(LatencyStages.Normalize, () => _inner.NormalizeAsync(question, cancellationToken));
    }
}
