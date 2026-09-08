using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using StackExchange.Redis;

namespace RAG.Class.Caching.Redis
{
    /// <summary>
    /// Cài đặt <see cref="IRedisConnection"/>: kết nối lười + bộ ngắt mạch, và không bao giờ ném
    /// ra ngoài.
    /// </summary>
    public sealed class RedisConnectionProvider : IRedisConnection, IDisposable
    {
        private readonly SemanticAnswerCacheConfig _config;
        private readonly ILogger<RedisConnectionProvider> _logger;

        /// <summary>Nối tiếp các lần dựng kết nối để hai request đồng thời không cùng mở hai multiplexer.</summary>
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        private ConnectionMultiplexer? _multiplexer;

        /// <summary>Số lần thất bại liên tiếp. Đọc/ghi qua <see cref="Interlocked"/> vì nhiều request chạy song song.</summary>
        private int _consecutiveFailures;

        /// <summary>Thời điểm sớm nhất được phép thử lại, tính bằng tick của <see cref="Environment.TickCount64"/>.</summary>
        private long _circuitOpenUntilTicks;

        /// <summary>Mạch đang mở hay không, để chỉ ghi log MỘT lần lúc chuyển trạng thái.</summary>
        private bool _circuitOpen;

        public RedisConnectionProvider(IOptions<SemanticAnswerCacheConfig> options,
                                       ILogger<RedisConnectionProvider> logger)
        {
            _config = options.Value;
            _logger = logger;
        }

        public void Dispose()
        {
            _multiplexer?.Dispose();
            _connectLock.Dispose();
        }

        public async Task<IDatabase?> TryGetDatabaseAsync(CancellationToken cancellationToken = default)
        {
            // Mạch đang mở thì về ngay, không chạm mạng. Đây chính là chỗ giữ cho một Redis đã
            // chết không biến thành khoản phụ thu ConnectTimeout trên MỌI request.
            if (IsCircuitOpen())
                return null;

            var existing = _multiplexer;
            if (existing is not null && existing.IsConnected)
                return existing.GetDatabase(_config.Database);

            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                // Kiểm lại sau khi giành được khóa: request khác có thể vừa kết nối xong.
                existing = _multiplexer;
                if (existing is not null && existing.IsConnected)
                    return existing.GetDatabase(_config.Database);

                if (IsCircuitOpen())
                    return null;

                existing?.Dispose();
                _multiplexer = await ConnectionMultiplexer.ConnectAsync(BuildOptions());

                // KHÔNG gọi ReportSuccess ở đây. AbortOnConnectFail = false nghĩa là ConnectAsync
                // THÀNH CÔNG ngay cả khi không nối được tới Redis — nó trả về một multiplexer sẽ
                // tự thử lại ở nền. Nên "connect xong" hoàn toàn không phải bằng chứng Redis còn
                // sống, và coi nó là thành công sẽ đặt lại bộ đếm lỗi ở MỖI request, khiến ngắt
                // mạch không bao giờ đủ ngưỡng để mở. Chỉ một lệnh chạy trót lọt mới là bằng
                // chứng, và chỗ báo điều đó là RedisSemanticAnswerCache sau mỗi thao tác.
                if (!_multiplexer.IsConnected)
                {
                    ReportFailure(new RedisConnectionException(
                        ConnectionFailureType.UnableToConnect,
                        "Không nối được tới Redis của cache ngữ nghĩa."));

                    return null;
                }

                return _multiplexer.GetDatabase(_config.Database);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
                return null;
            }
            finally
            {
                _connectLock.Release();
            }
        }

        public void ReportFailure(Exception exception)
        {
            var failures = Interlocked.Increment(ref _consecutiveFailures);

            if (failures < _config.MaxConsecutiveFailures)
                return;

            Volatile.Write(ref _circuitOpenUntilTicks,
                Environment.TickCount64 + _config.FailureCooldownSeconds * 1000L);

            // Ghi log CHỈ ở lần chuyển trạng thái. Ở 50 rps, một Redis chết mà log mỗi request sẽ
            // đẩy ra 50 dòng cảnh báo mỗi giây và chôn vùi mọi thứ khác trong log.
            if (_circuitOpen)
                return;

            _circuitOpen = true;

            _logger.LogWarning(exception,
                "Ngắt mạch cache ngữ nghĩa sau {Failures} lần lỗi liên tiếp; nghỉ {Cooldown}s rồi thử lại. " +
                "Đường trả lời vẫn chạy bình thường qua Qdrant + LLM.",
                failures, _config.FailureCooldownSeconds);
        }

        public void ReportSuccess()
        {
            if (Interlocked.Exchange(ref _consecutiveFailures, 0) == 0 && !_circuitOpen)
                return;

            Volatile.Write(ref _circuitOpenUntilTicks, 0);

            if (!_circuitOpen)
                return;

            _circuitOpen = false;
            _logger.LogInformation("Cache ngữ nghĩa đã kết nối lại được; đóng ngắt mạch.");
        }

        private bool IsCircuitOpen()
        {
            var until = Volatile.Read(ref _circuitOpenUntilTicks);

            if (until == 0)
                return false;

            if (Environment.TickCount64 < until)
                return true;

            // Hết thời gian nghỉ: cho MỘT request đi qua để thăm dò. Không đặt lại bộ đếm lỗi ở
            // đây — nếu request thăm dò cũng hỏng thì ReportFailure mở lại mạch ngay lập tức.
            Volatile.Write(ref _circuitOpenUntilTicks, 0);
            return false;
        }

        private ConfigurationOptions BuildOptions()
        {
            var options = ConfigurationOptions.Parse(_config.ConnectionString);

            // BẮT BUỘC false. Mặc định (true) khiến ConnectAsync NÉM khi Redis chưa lên, và tệ hơn
            // là multiplexer sau đó KHÔNG tự kết nối lại — tức là Redis lên muộn hơn app một giây
            // cũng đủ để cache chết vĩnh viễn cho tới lần khởi động lại.
            options.AbortOnConnectFail = false;

            options.ConnectTimeout = _config.ConnectTimeoutMs;
            options.ConnectRetry = _config.ConnectRetry;
            options.SyncTimeout = _config.OperationTimeoutMs;
            options.AsyncTimeout = _config.OperationTimeoutMs;
            options.ClientName = AnswerCacheFields.ClientName;

            return options;
        }
    }
}
