using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Làm nóng kết nối Neo4j ngay sau khi ứng dụng khởi động.
    /// <para>
    /// Cố tình KHÔNG chặn startup, cùng lý do với <c>SemanticRouterWarmupService</c>: Neo4j chết
    /// chỉ làm câu trả lời tụt về RAG thuần, không được làm container crash-loop.
    /// </para>
    /// </summary>
    public sealed class GraphWarmupService : BackgroundService
    {
        private readonly IGraphWarmup _graph;
        private readonly Neo4jConfig _config;
        private readonly ILogger<GraphWarmupService> _logger;

        public GraphWarmupService(IGraphWarmup graph,
                                  IOptions<Neo4jConfig> options,
                                  ILogger<GraphWarmupService> logger)
        {
            _graph = graph;
            _config = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Nhả luồng ngay để host tiếp tục khởi động rồi mới gọi ra ngoài mạng.
            await Task.Yield();

            var delay = TimeSpan.FromSeconds(_config.WarmupRetryDelaySeconds);

            for (var attempt = 1; attempt <= _config.WarmupMaxAttempts; attempt++)
            {
                try
                {
                    if (await _graph.WarmUpAsync(stoppingToken))
                    {
                        _logger.LogInformation("Đã làm nóng kết nối Neo4j (lần {Attempt}).", attempt);
                        return;
                    }

                    _logger.LogWarning("Làm nóng Neo4j chưa thành công (lần {Attempt}/{Max}).",
                        attempt, _config.WarmupMaxAttempts);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Làm nóng Neo4j lỗi (lần {Attempt}/{Max}).", attempt, _config.WarmupMaxAttempts);
                }

                if (attempt == _config.WarmupMaxAttempts)
                    break;

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            _logger.LogWarning("Từ bỏ làm nóng Neo4j sau {Max} lần thử. Vài câu hỏi đầu có thể trả lời " +
                               "không kèm đồ thị cho tới khi kết nối ổn định.", _config.WarmupMaxAttempts);
        }
    }
}
