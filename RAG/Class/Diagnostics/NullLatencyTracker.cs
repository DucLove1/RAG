using RAG.Interface;

namespace RAG.Class.Diagnostics
{
    /// <summary>
    /// Null Object cho khi <c>Latency:Enabled = false</c>, cùng vai trò với <c>NullQueryCache</c> và
    /// <c>NullSemanticAnswerCache</c>.
    /// <para>
    /// Thực tế nó gần như không bao giờ được gọi tới: khi tính năng tắt, DI không bọc decorator đo
    /// giờ nào cả nên không ai gọi. Nó tồn tại để phần đăng ký luôn thỏa được
    /// <see cref="ILatencyTracker"/> — nhờ vậy thêm một consumer mới sẽ không nổ lúc khởi động chỉ
    /// vì ai đó tắt tính năng này trong cấu hình.
    /// </para>
    /// </summary>
    public sealed class NullLatencyTracker : ILatencyTracker, ILatencySessionFactory
    {
        private static readonly IDisposable NoSession = new NoOpSession();

        public IDisposable Begin(string operation) => NoSession;

        public Task<T> TrackAsync<T>(string stage, Func<Task<T>> operation) => operation();

        public Task TrackAsync(string stage, Func<Task> operation) => operation();

        public void Tag(string name, string value)
        {
        }

        public string? GetTag(string name) => null;

        private sealed class NoOpSession : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
