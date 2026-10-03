using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo riêng lượt gọi HTTP tới Jev, tách khỏi stage <see cref="LatencyStages.Route"/> vốn còn
    /// gồm cả cache quyết định. Số đo gồm mọi vòng xoay key khi gặp 429.
    /// </summary>
    public sealed class TimedJevClient : IJevClient
    {
        private readonly IJevClient _inner;
        private readonly ILatencyTracker _latency;

        public TimedJevClient(IJevClient inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public Task<SystemOneAnswer> AskChoiceAsync(string state,
                                                    string questionId,
                                                    SystemOneQuestion question,
                                                    CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(LatencyStages.RouteJev,
                () => _inner.AskChoiceAsync(state, questionId, question, cancellationToken));
    }
}
