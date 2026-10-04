using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo một lượt phân loại của router thật: từ lúc gửi câu hỏi đi tới lúc đã chọn xong route
    /// (hoặc null → RAG).
    /// <para>
    /// Đặt BÊN TRONG cache quyết định, nên stage chỉ xuất hiện khi router thật sự được gọi. Mọi
    /// chiến lược dùng chung lớp này để cửa sổ đo của chúng giống hệt nhau — so Jev với LLM mới
    /// công bằng. Tên stage nhận qua constructor để mỗi chiến lược một cột riêng trong log.
    /// </para>
    /// </summary>
    public sealed class TimedRouteClassifier : ISemanticRouter
    {
        private readonly ISemanticRouter _inner;
        private readonly ILatencyTracker _latency;
        private readonly string _stage;

        public TimedRouteClassifier(ISemanticRouter inner, ILatencyTracker latency, string stage)
        {
            _inner = inner;
            _latency = latency;
            _stage = stage;
        }

        public Task<RouteMatch?> RouteAsync(string question, CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(_stage, () => _inner.RouteAsync(question, cancellationToken));
    }
}
