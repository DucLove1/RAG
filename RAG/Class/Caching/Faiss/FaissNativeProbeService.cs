using RAG.Interface;

namespace RAG.Class.Caching.Faiss
{
    /// <summary>
    /// Thăm dò native của FAISS một lần ngay sau khi ứng dụng khởi động.
    /// <para>
    /// Cố tình KHÔNG chặn startup và KHÔNG làm app sập, cùng tinh thần fail-open của chính cache:
    /// thiếu FAISS chỉ làm mất tối ưu hóa, không được làm container crash-loop. Việc của service
    /// này chỉ là biến một lỗi VÔ HÌNH ("cache không bao giờ trúng") thành một dòng log Error có
    /// chỉ dẫn cách sửa.
    /// </para>
    /// <para>
    /// Service riêng chứ không gộp vào <see cref="AnswerCachePersistenceService"/>: service đó chỉ
    /// được đăng ký khi <c>PersistPath</c> khác rỗng, còn thăm dò phải chạy cả khi cache chỉ sống
    /// trong RAM.
    /// </para>
    /// </summary>
    public sealed class FaissNativeProbeService : BackgroundService
    {
        private readonly IAnswerCacheNativeProbe _probe;
        private readonly ILogger<FaissNativeProbeService> _logger;

        public FaissNativeProbeService(IAnswerCacheNativeProbe probe, ILogger<FaissNativeProbeService> logger)
        {
            _probe = probe;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Nhả luồng ngay để host khởi động xong rồi mới nạp thư viện native.
            await Task.Yield();

            if (_probe.TryProbe(out var error))
            {
                _logger.LogInformation("Native FAISS đã nạp được; cache ngữ nghĩa provider Faiss sẵn sàng.");
                return;
            }

            _logger.LogError(error,
                "Không nạp được native FAISS — cache ngữ nghĩa sẽ LUÔN TRƯỢT cho tới khi sửa. Trên Linux, " +
                "libfaiss.so cần libopenblas.so.0 (gói libopenblas0-pthread), libgomp.so.1 (gói libgomp1) và " +
                "glibc >= 2.38: xem khối \"THU VIEN HE THONG\" trong Dockerfile. Trên Windows cần Microsoft " +
                "Visual C++ Redistributable. Không cài được thì đổi SemanticAnswerCache:Provider sang Redis.");
        }
    }
}
