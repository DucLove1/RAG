using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo bước phát hiện trúng điểm yếu.
    /// <para>
    /// Stage này gần như bằng 0 với NPC không có mục trong <c>WeakPoint:Targets</c> (bộ phát hiện
    /// thoát ngay ở phép tra từ điển) và bằng cả một lượt gọi LLM với NPC có. Nhìn con số này là
    /// biết ngay node có đang đốt một lượt gọi thừa cho mọi câu hỏi hay không.
    /// </para>
    /// </summary>
    public sealed class TimedWeakPointDetector : IWeakPointDetector
    {
        private readonly IWeakPointDetector _inner;
        private readonly ILatencyTracker _latency;

        public TimedWeakPointDetector(IWeakPointDetector inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public async Task<WeakPointMatch?> DetectAsync(string npcName,
                                                       string question,
                                                       CancellationToken cancellationToken = default)
        {
            var match = await _latency.TrackAsync(
                LatencyStages.WeakPoint, () => _inner.DetectAsync(npcName, question, cancellationToken));

            // KHÔNG ghi nội dung câu chốt hay lời thoại vào log: đó là nội dung cốt truyện, và log
            // của server game thì thường được xem bởi nhiều người hơn là người chơi.
            _latency.Tag(LatencyTags.WeakPointHit,
                match is not null ? LatencyTagValues.True : LatencyTagValues.False);

            return match;
        }
    }
}
