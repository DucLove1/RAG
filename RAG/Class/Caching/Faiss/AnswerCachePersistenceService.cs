using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Caching.Faiss
{
    /// <summary>
    /// Nạp cache câu trả lời từ đĩa lúc khởi động, rồi quét entry hết hạn và ghi lại định kỳ, cùng
    /// một lần cuối lúc tắt.
    /// <para>
    /// Ghi theo kiểu write-behind chứ KHÔNG ghi ở mỗi request: mỗi lần ghi là ghi lại cả file, nên
    /// ghi theo request sẽ là O(n) trên từng request — không dùng được. Cùng lý do và cùng hình
    /// dạng với <see cref="QueryCachePersistenceService"/>.
    /// </para>
    /// <para>
    /// Vì sao phải có flush định kỳ chứ không chỉ flush lúc tắt: container thường bị dừng bằng
    /// SIGKILL (ví dụ <c>docker kill</c>, hoặc quá thời gian chờ của <c>docker stop</c>), lúc đó
    /// không có shutdown êm nào chạy cả. Flush định kỳ là lưới an toàn cho trường hợp đó.
    /// </para>
    /// <para>
    /// Chỉ được đăng ký cho provider FAISS, và chỉ khi <c>PersistPath</c> khác rỗng. Đây là một
    /// trong ba lý do việc chọn provider phải là <c>switch</c> ở composition root chứ không phải
    /// Keyed Services: <c>AddHostedService</c> KHÔNG keyed được, nên đăng ký sẵn sẽ khiến service
    /// này chạy và ghi một cache rỗng đè lên file, ngay cả khi đang dùng Redis.
    /// </para>
    /// </summary>
    public sealed class AnswerCachePersistenceService : BackgroundService
    {
        private readonly IPersistableAnswerCache _cache;
        private readonly IAnswerCacheStore _store;
        private readonly SemanticAnswerCacheFaissConfig _config;
        private readonly ILogger<AnswerCachePersistenceService> _logger;

        /// <summary>Giá trị ChangeCount tại lần flush gần nhất; khác đi nghĩa là có gì đó mới.</summary>
        private long _lastFlushedChangeCount;

        public AnswerCachePersistenceService(IPersistableAnswerCache cache,
                                             IAnswerCacheStore store,
                                             IOptions<SemanticAnswerCacheFaissConfig> options,
                                             ILogger<AnswerCachePersistenceService> logger)
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
                _lastFlushedChangeCount = _cache.ChangeCount;

                _logger.LogInformation("Cache câu trả lời đã sẵn sàng với {Count} entry nạp từ đĩa.", imported);
            }
            catch (OperationCanceledException)
            {
                // Đang tắt máy, không có gì phải xử lý.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không nạp được cache câu trả lời, bắt đầu với cache rỗng.");
            }
        }

        private async Task FlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Quét TRƯỚC khi ghi, không phải sau: entry đã hết hạn nhờ vậy không bao giờ đi
                // xuống đĩa, và trần MaxEntries được ép ở đúng một chỗ thay vì trên đường nóng.
                // Quét có xoá được gì thì tự tăng ChangeCount, nên nó cũng tự kéo theo một lần ghi.
                var swept = _cache.SweepExpired(DateTime.UtcNow);

                if (swept > 0)
                    _logger.LogDebug("Đã dọn {Count} entry cache câu trả lời hết hạn hoặc vượt trần.", swept);

                var changeCount = _cache.ChangeCount;

                // Không có gì mới thì đừng ghi đĩa vô ích.
                if (changeCount == _lastFlushedChangeCount)
                {
                    _logger.LogDebug("Cache câu trả lời không có thay đổi, bỏ qua lần flush này.");
                    return;
                }

                var snapshot = _cache.ExportSnapshot(_config.MaxEntries);

                if (await _store.SaveAsync(_cache.Fingerprint, snapshot, cancellationToken))
                    _lastFlushedChangeCount = changeCount;
            }
            catch (OperationCanceledException)
            {
                // Hết thời gian chờ lúc shutdown. Mất phần chưa flush là chấp nhận được với cache.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi ghi cache câu trả lời xuống đĩa.");
            }
        }
    }
}
