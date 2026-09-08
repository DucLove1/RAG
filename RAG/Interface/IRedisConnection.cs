using StackExchange.Redis;

namespace RAG.Interface
{
    /// <summary>
    /// Cổng vào Redis đã bọc sẵn luật fail-open. Trả <c>null</c> nghĩa là "Redis đang không dùng
    /// được", chứ không ném.
    /// <para>
    /// Tồn tại vì đăng ký thẳng <see cref="IConnectionMultiplexer"/> làm singleton là một cái bẫy:
    /// factory của DI mà ném (sai chuỗi kết nối, hỏng DNS) thì MỌI lần resolve <c>AskPipeline</c>
    /// đều hỏng, và Redis chết kéo theo cả app chết — đúng thứ mà fail-open sinh ra để ngăn. Luật
    /// "không bao giờ ném" phải nằm DƯỚI đồ thị DI chứ không phải rải trong từng chỗ gọi.
    /// </para>
    /// <para>
    /// Cài đặt còn ôm luôn bộ ngắt mạch. Không có nó, một Redis đã chết vẫn khiến MỌI request trả
    /// trọn hạn kết nối trước khi rơi xuống đường thường — tức là cache làm app CHẬM HƠN so với
    /// khi không có cache.
    /// </para>
    /// </summary>
    public interface IRedisConnection
    {
        /// <summary>
        /// Lấy database để làm việc, hoặc <c>null</c> khi chưa kết nối được / đang ngắt mạch.
        /// Không bao giờ ném, trừ khi chính <paramref name="cancellationToken"/> bị hủy.
        /// </summary>
        Task<IDatabase?> TryGetDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>Báo một thao tác vừa thất bại, để bộ ngắt mạch đếm và mở khi đủ ngưỡng.</summary>
        void ReportFailure(Exception exception);

        /// <summary>Báo một thao tác vừa thành công, để đóng lại mạch đang mở.</summary>
        void ReportSuccess();
    }
}
