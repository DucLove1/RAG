using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo bước định tuyến và ghi lại route đã khớp.
    /// <para>
    /// Nhãn route là thứ giải thích cả phần còn lại của dòng log: khớp route thì mọi stage từ nhúng
    /// trở xuống KHÔNG chạy, và không có nhãn này thì một phiên chỉ có hai stage trông y hệt một
    /// phiên chết giữa chừng.
    /// </para>
    /// </summary>
    public sealed class TimedSemanticRouter : ISemanticRouter
    {
        private readonly ISemanticRouter _inner;
        private readonly ILatencyTracker _latency;

        public TimedSemanticRouter(ISemanticRouter inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public async Task<RouteMatch?> RouteAsync(string question, CancellationToken cancellationToken = default)
        {
            var route = await _latency.TrackAsync(
                LatencyStages.Route, () => _inner.RouteAsync(question, cancellationToken));

            _latency.Tag(LatencyTags.Route, route?.Name ?? LatencyTagValues.None);

            return route;
        }
    }
}
