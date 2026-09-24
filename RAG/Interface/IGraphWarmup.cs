namespace RAG.Interface
{
    /// <summary>
    /// Làm nóng kết nối tới kho đồ thị lúc khởi động.
    /// <para>
    /// Tách khỏi <see cref="IGraphStore"/> theo ISP: đường trả lời không bao giờ gọi nó, và dịch vụ
    /// làm nóng không cần biết gì khác. Cài đặt nằm trong lớp giữ driver, để dịch vụ khởi động
    /// không phải import <c>Neo4j.Driver</c>.
    /// </para>
    /// </summary>
    public interface IGraphWarmup
    {
        /// <returns><c>true</c> nếu đã kết nối được và chạy xong một lượt truy vấn thật.</returns>
        Task<bool> WarmUpAsync(CancellationToken cancellationToken = default);
    }
}
