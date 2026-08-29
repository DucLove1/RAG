using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Nạp cache hỏi đáp từ đĩa lúc khởi động, rồi ghi lại định kỳ và một lần cuối lúc tắt.
    /// <para>
    /// Ghi theo kiểu write-behind chứ KHÔNG ghi ở mỗi request: mỗi lần ghi là ghi lại cả file, nên
    /// ghi theo request sẽ là O(n) trên từng request — không dùng được.
    /// </para>
    /// <para>
    /// Vì sao phải có flush định kỳ chứ không chỉ flush lúc tắt: container thường bị dừng bằng
    /// SIGKILL (ví dụ <c>docker kill</c>, hoặc quá thời gian chờ của <c>docker stop</c>), lúc đó
    /// không có shutdown êm nào chạy cả. Flush định kỳ là lưới an toàn cho trường hợp đó.
    /// </para>
    /// </summary>
    public sealed class QueryCachePersistenceService : BackgroundService
    {
        private readonly MemoryQueryCache _cache;
        private readonly IQueryCacheStore _store;
        private readonly QueryCacheConfig _config;
        private readonly ILogger<QueryCachePersistenceService> _logger;

        /// <summary>Giá trị WriteCount tại lần flush gần nhất; khác đi nghĩa là có gì đó mới.</summary>
        private long _lastFlushedWriteCount;

        public QueryCachePersistenceService(MemoryQueryCache cache,
                                            IQueryCacheStore store,
                                            IOptions<QueryCacheConfig> options,
                                            ILogger<QueryCachePersistenceService> logger)
        {
            _cache = cache;
            _store = store;
            _config = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Nhả luồng ngay để host khởi động xong rồi mới chạm đĩa.
            await Task.Yield();

            await LoadAsync(stoppingToken);

            var interval = TimeSpan.FromSeconds(Math.Max(1, _config.FlushIntervalSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                await FlushAsync(stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Chạy khi shutdown êm (SIGTERM của Docker). Dùng token của shutdown chứ không phải
            // stoppingToken vốn đã bị huỷ ở thời điểm này.
            await FlushAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                var snapshot = await _store.LoadAsync(_cache.Fingerprint, cancellationToken);

                if (snapshot is null)
                    return;

                var imported = _cache.ImportSnapshot(snapshot);
                _lastFlushedWriteCount = _cache.WriteCount;

                _logger.LogInformation("Cache hỏi đáp đã sẵn sàng với {Count} entry nạp từ đĩa.", imported);
            }
            catch (OperationCanceledException)
            {
                // Đang tắt máy, không có gì phải xử lý.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không nạp được cache hỏi đáp, bắt đầu với cache rỗng.");
            }
        }

        private async Task FlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                var writeCount = _cache.WriteCount;

                // Không có gì mới thì đừng ghi đĩa vô ích.
                if (writeCount == _lastFlushedWriteCount)
                {
                    _logger.LogDebug("Cache hỏi đáp không có thay đổi, bỏ qua lần flush này.");
                    return;
                }

                var snapshot = _cache.ExportSnapshot(_config.MaxPersistedEntries);

                if (await _store.SaveAsync(_cache.Fingerprint, snapshot, cancellationToken))
                    _lastFlushedWriteCount = writeCount;
            }
            catch (OperationCanceledException)
            {
                // Hết thời gian chờ lúc shutdown. Mất phần chưa flush là chấp nhận được với cache.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi ghi cache hỏi đáp xuống đĩa.");
            }
        }
    }
}
