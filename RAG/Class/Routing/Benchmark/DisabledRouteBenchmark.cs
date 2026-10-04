using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Routing.Benchmark
{
    /// <summary>
    /// Thay cho benchmark thật khi nó tắt hoặc chạy ngoài Development. Chặn ở tầng đăng ký, cùng
    /// mẫu với <c>DisabledGraphAdmin</c>; ném 404 chứ không 403 để không xác nhận endpoint tồn tại.
    /// </summary>
    public sealed class DisabledRouteBenchmark : IRouteBenchmark
    {
        private readonly string _message;

        public DisabledRouteBenchmark(IOptions<RouteBenchmarkConfig> options)
        {
            _message = options.Value.DisabledMessage;
        }

        public Task<RouteBenchmarkReport> RunAsync(bool onlyMistakes, CancellationToken cancellationToken = default) =>
            throw new RouteBenchmarkDisabledException(_message);
    }
}
