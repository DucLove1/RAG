using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo cache câu trả lời theo ngữ nghĩa.
    /// <para>
    /// Đây là stage đáng ngờ nhất của cả pipeline: cache này fail-open và tự nuốt lỗi, nên một Redis
    /// chết trông giống hệt một cache nguội. <c>SemanticAnswerCacheStats.Errors</c> đếm số lần lỗi,
    /// nhưng chỉ có thời gian mới cho thấy mỗi request đang trả bao nhiêu mili-giây cho một tầng
    /// không trả về gì — ví dụ đúng bằng <c>OperationTimeoutMs</c> ở mọi request.
    /// </para>
    /// </summary>
    public sealed class TimedSemanticAnswerCache : ISemanticAnswerCache
    {
        private readonly ISemanticAnswerCache _inner;
        private readonly ILatencyTracker _latency;

        public TimedSemanticAnswerCache(ISemanticAnswerCache inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public async Task<CachedAnswer?> TryGetAsync(SemanticAnswerQuery query,
                                                     CancellationToken cancellationToken = default)
        {
            var cached = await _latency.TrackAsync(
                LatencyStages.AnswerCacheGet, () => _inner.TryGetAsync(query, cancellationToken));

            _latency.Tag(LatencyTags.AnswerCache,
                cached is not null ? LatencyTagValues.Hit : LatencyTagValues.Miss);

            return cached;
        }

        public Task SetAsync(SemanticAnswerQuery query,
                             string answer,
                             bool hasContext,
                             CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(
                LatencyStages.AnswerCacheSet, () => _inner.SetAsync(query, answer, hasContext, cancellationToken));
    }
}
