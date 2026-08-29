using Microsoft.Extensions.Options;
using RAG.Class.Config;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Nạp trước vector của các câu mẫu ngay sau khi ứng dụng khởi động.
    /// <para>
    /// Cố tình KHÔNG chặn startup: một lần nhà cung cấp embedding sập chỉ khiến node định tuyến
    /// tạm nghỉ (mọi request đi đường RAG), chứ không làm container crash-loop.
    /// Warm-up chạy dưới token của host chứ không phải của request, nên một client ngắt kết nối
    /// không thể làm hỏng cache dùng chung.
    /// </para>
    /// </summary>
    public sealed class SemanticRouterWarmupService : BackgroundService
    {
        private readonly EmbeddingSemanticRouter _router;
        private readonly SemanticRouterConfig _config;
        private readonly ILogger<SemanticRouterWarmupService> _logger;

        public SemanticRouterWarmupService(EmbeddingSemanticRouter router,
                                           IOptions<SemanticRouterConfig> options,
                                           ILogger<SemanticRouterWarmupService> logger)
        {
            _router = router;
            _config = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Nhả luồng ngay để host tiếp tục khởi động rồi mới gọi ra ngoài mạng.
            await Task.Yield();

            var maxAttempts = Math.Max(1, _config.WarmupMaxAttempts);
            var delay = TimeSpan.FromSeconds(Math.Max(1, _config.WarmupRetryDelaySeconds));

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                try
                {
                    if (await _router.TryBuildAsync(stoppingToken))
                        return;

                    _logger.LogWarning("Warm-up semantic router chưa thành công (lần {Attempt}/{Max}).",
                        attempt, maxAttempts);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Warm-up semantic router lỗi (lần {Attempt}/{Max}).", attempt, maxAttempts);
                }

                if (attempt == maxAttempts)
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

            _logger.LogWarning("Từ bỏ warm-up sau {Max} lần thử. Node định tuyến sẽ không hoạt động " +
                               "cho tới lần khởi động sau; mọi câu hỏi đi đường RAG.", maxAttempts);
        }
    }
}
