using System.Diagnostics;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Diagnostics
{
    /// <summary>
    /// Giữ phiên đo hiện hành trong <see cref="AsyncLocal{T}"/>, đúng cách <c>Activity.Current</c>
    /// hoạt động.
    /// <para>
    /// PHẢI làm theo kiểu ambient chứ không phải service scoped, vì toàn bộ stack RAG là singleton
    /// (xem <c>RagStackServiceCollectionExtensions</c>): một decorator singleton không thể nhận một
    /// service scoped qua constructor — đó là captive dependency, và container sẽ nổ lúc khởi động.
    /// </para>
    /// <para>
    /// <see cref="AsyncLocal{T}"/> chảy theo luồng logic chứ không theo thread, nên phiên vẫn đúng
    /// sau mỗi lần <c>await</c> nhảy thread. Đổi lại, nó cũng chảy sang MỌI task con được khởi trong
    /// phiên — vì vậy <see cref="LatencySession"/> có khóa.
    /// </para>
    /// </summary>
    public sealed class AsyncLocalLatencyTracker : ILatencyTracker, ILatencySessionFactory
    {
        private static readonly AsyncLocal<LatencySession?> Current = new();

        private readonly ILatencyReporter _reporter;
        private readonly HashSet<string> _disabledStages;

        public AsyncLocalLatencyTracker(ILatencyReporter reporter, IOptions<LatencyConfig> config)
        {
            _reporter = reporter;
            _disabledStages = new HashSet<string>(config.Value.DisabledStages, StringComparer.OrdinalIgnoreCase);
        }

        public ILatencySession Begin(string operation)
        {
            var previous = Current.Value;
            var session = new LatencySession(operation);

            Current.Value = session;

            return new SessionHandle(this, session, previous);
        }

        public async Task<T> TrackAsync<T>(string stage, Func<Task<T>> operation)
        {
            var session = ActiveSessionFor(stage);

            if (session is null)
                return await operation();

            var startedAt = Stopwatch.GetTimestamp();

            // finally chứ không phải chỉ đường thành công: một request ném ra sau 30 giây chính là
            // request cần biết 30 giây đó nằm ở chặng nào.
            try
            {
                return await operation();
            }
            finally
            {
                session.Record(stage, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            }
        }

        public async Task TrackAsync(string stage, Func<Task> operation)
        {
            var session = ActiveSessionFor(stage);

            if (session is null)
            {
                await operation();
                return;
            }

            var startedAt = Stopwatch.GetTimestamp();

            try
            {
                await operation();
            }
            finally
            {
                session.Record(stage, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            }
        }

        public void Record(string stage, double milliseconds) =>
            ActiveSessionFor(stage)?.Record(stage, milliseconds);

        public void Tag(string name, string value) => Current.Value?.Tag(name, value);

        public string? GetTag(string name) => Current.Value?.GetTag(name);

        /// <summary>
        /// Phiên đang mở, hoặc <c>null</c> khi ngoài phiên / stage bị tắt qua cấu hình. Trả về
        /// <c>null</c> ở cả hai trường hợp là có chủ ý: caller chỉ cần biết "có đo hay không".
        /// </summary>
        private LatencySession? ActiveSessionFor(string stage)
        {
            var session = Current.Value;

            if (session is null || _disabledStages.Count == 0)
                return session;

            return _disabledStages.Contains(stage) ? null : session;
        }

        /// <summary>
        /// Đóng phiên và đẩy báo cáo đi. Khôi phục về phiên trước đó chứ không phải gán <c>null</c>:
        /// nếu về sau có một phiên lồng trong phiên (nạp dữ liệu gọi lại đường trả lời chẳng hạn),
        /// gán <c>null</c> sẽ giết luôn phiên ngoài và mọi stage sau đó biến mất khỏi log.
        /// </summary>
        private sealed class SessionHandle : ILatencySession
        {
            private readonly AsyncLocalLatencyTracker _owner;
            private readonly LatencySession _session;
            private readonly LatencySession? _previous;
            private bool _disposed;

            public SessionHandle(AsyncLocalLatencyTracker owner, LatencySession session, LatencySession? previous)
            {
                _owner = owner;
                _session = session;
                _previous = previous;
            }

            public void Activate() => Current.Value = _session;

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                Current.Value = _previous;

                _owner._reporter.Report(_session.Build());
            }
        }
    }
}
