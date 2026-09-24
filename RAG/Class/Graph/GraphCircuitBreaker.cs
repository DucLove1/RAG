namespace RAG.Class.Graph
{
    /// <summary>
    /// Bộ ngắt mạch cho tầng đồ thị.
    /// <para>
    /// Là bản tách không dính driver của cơ chế đã có trong <c>RedisConnectionProvider</c>. Chép
    /// logic thay vì dùng lại lớp kia là có chủ đích: lớp kia gắn chặt với vòng đời
    /// <c>ConnectionMultiplexer</c> của StackExchange.Redis, còn ở đây kết nối do
    /// <c>Neo4j.Driver</c> tự quản. Thứ đáng dùng chung là ba quyết định bên dưới, không phải đoạn
    /// mã kết nối.
    /// </para>
    /// <para>
    /// Ba quyết định đó: mạch hở thì về NGAY không chạm mạng (nếu không, một Neo4j chết thành khoản
    /// phụ thu <c>ConnectTimeout</c> trên 100% câu hỏi); log CHỈ ở lần chuyển trạng thái (log mỗi
    /// request sẽ chôn vùi mọi dòng khác); và hết giờ nghỉ thì cho ĐÚNG MỘT request đi thăm dò,
    /// không đặt lại bộ đếm lỗi — thăm dò hỏng là mạch mở lại ngay.
    /// </para>
    /// </summary>
    public sealed class GraphCircuitBreaker
    {
        private readonly int _maxConsecutiveFailures;
        private readonly int _cooldownSeconds;
        private readonly ILogger _logger;

        /// <summary>Số lần hỏng liên tiếp. Đọc/ghi qua <see cref="Interlocked"/> vì nhiều request chạy song song.</summary>
        private int _consecutiveFailures;

        /// <summary>Thời điểm sớm nhất được phép thử lại, theo <see cref="Environment.TickCount64"/>.</summary>
        private long _openUntilTicks;

        /// <summary>Mạch đang hở hay không, để chỉ ghi log MỘT lần lúc chuyển trạng thái.</summary>
        private bool _open;

        public GraphCircuitBreaker(int maxConsecutiveFailures, int cooldownSeconds, ILogger logger)
        {
            _maxConsecutiveFailures = maxConsecutiveFailures;
            _cooldownSeconds = cooldownSeconds;
            _logger = logger;
        }

        public bool IsOpen()
        {
            var until = Volatile.Read(ref _openUntilTicks);

            if (until == 0)
                return false;

            if (Environment.TickCount64 < until)
                return true;

            // Hết giờ nghỉ: cho MỘT request đi thăm dò. Không đặt lại bộ đếm lỗi ở đây — request
            // thăm dò mà hỏng thì ReportFailure mở lại mạch ngay lập tức.
            Volatile.Write(ref _openUntilTicks, 0);
            return false;
        }

        public void ReportFailure(Exception exception)
        {
            var failures = Interlocked.Increment(ref _consecutiveFailures);

            if (failures < _maxConsecutiveFailures)
                return;

            Volatile.Write(ref _openUntilTicks, Environment.TickCount64 + _cooldownSeconds * 1000L);

            if (_open)
                return;

            _open = true;

            _logger.LogWarning(exception,
                "Ngắt mạch đồ thị tri thức sau {Failures} lần lỗi liên tiếp; nghỉ {Cooldown}s rồi thử lại. " +
                "Đường trả lời vẫn chạy bình thường bằng RAG thuần.",
                failures, _cooldownSeconds);
        }

        public void ReportSuccess()
        {
            if (Interlocked.Exchange(ref _consecutiveFailures, 0) == 0 && !_open)
                return;

            Volatile.Write(ref _openUntilTicks, 0);

            if (!_open)
                return;

            _open = false;
            _logger.LogInformation("Đồ thị tri thức đã truy vấn lại được; đóng ngắt mạch.");
        }
    }
}
