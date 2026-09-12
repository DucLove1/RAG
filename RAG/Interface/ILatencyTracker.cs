namespace RAG.Interface
{
    /// <summary>
    /// Số liệu của MỘT stage trong một phiên đo.
    /// </summary>
    /// <param name="Calls">
    /// Số lần stage được gọi trong phiên. Lớn hơn 1 là chuyện bình thường chứ không phải lỗi: nhánh
    /// định tuyến bằng embedding nhúng câu hỏi một lần cho router rồi nhánh truy hồi nhúng lần nữa
    /// (lần sau trúng cache). Gộp thành một con số mà giấu đi số lần gọi thì một stage 200ms gọi hai
    /// lần trông y hệt một stage 400ms gọi một lần.
    /// </param>
    public sealed record LatencyStageSample(string Stage, double Milliseconds, int Calls);

    /// <summary>
    /// Toàn bộ số liệu của một phiên, chốt lại lúc phiên đóng.
    /// </summary>
    /// <param name="TotalMilliseconds">
    /// Thời gian tường của cả phiên. CỐ TÌNH đo riêng chứ không cộng các stage lại: phần chênh
    /// chính là thời gian nằm ngoài mọi stage (dựng prompt, nối ngữ cảnh, chờ thread pool), và đó
    /// đúng là thứ sẽ không ai nhìn thấy nếu tổng được định nghĩa bằng tổng các phần.
    /// </param>
    public sealed record LatencyReport(string Operation,
                                       double TotalMilliseconds,
                                       IReadOnlyList<LatencyStageSample> Stages,
                                       IReadOnlyDictionary<string, string> Tags);

    /// <summary>
    /// Thứ mà các decorator đo giờ dùng.
    /// <para>
    /// BẤT BIẾN: không method nào ở đây được phép nuốt hay đổi hành vi của operation được bọc. Đo
    /// giờ là mối quan tâm quan sát, một lỗi ở tầng này mà làm hỏng câu trả lời của NPC thì cái giá
    /// đắt hơn nhiều so với việc không có số liệu. Exception của operation được ném tiếp nguyên vẹn,
    /// và thời gian tới lúc ném vẫn được ghi.
    /// </para>
    /// <para>
    /// Ngoài phiên đo (warmup router, hosted service, hay khi tính năng bị tắt) thì mọi method chỉ
    /// gọi thẳng operation — không cấp phát, không đo, không log.
    /// </para>
    /// </summary>
    public interface ILatencyTracker
    {
        Task<T> TrackAsync<T>(string stage, Func<Task<T>> operation);

        Task TrackAsync(string stage, Func<Task> operation);

        /// <summary>
        /// Gắn nhãn cho phiên hiện hành (nhánh nào chạy, cache trúng hay trượt...). No-op ngoài phiên.
        /// <para>
        /// Nhãn trùng tên thì ghi đè: một request chỉ có một nhánh, một kết quả cache. Cộng dồn như
        /// stage sẽ biến "trúng" và "trượt" của hai lần tra thành một chuỗi vô nghĩa.
        /// </para>
        /// </summary>
        void Tag(string name, string value);

        /// <summary>
        /// Đọc một nhãn của phiên hiện hành, <c>null</c> nếu chưa có hoặc đang ngoài phiên.
        /// <para>
        /// Có mặt để nhánh nào chạy được suy ra từ chính các nhãn mà những decorator ở dưới đã gắn,
        /// thay vì phải sửa <c>AskPipeline</c> để nó tự khai báo. Đây là cách duy nhất biết được
        /// nhánh mà vẫn giữ lớp lõi sạch.
        /// </para>
        /// </summary>
        string? GetTag(string name);
    }

    /// <summary>
    /// Mở một phiên đo. Tách khỏi <see cref="ILatencyTracker"/> theo ISP: bảy decorator chỉ ghi số
    /// liệu và không được phép mở hay đóng phiên — chỉ hai decorator đứng ở đầu luồng
    /// (<c>IAskService</c>, <c>IIngestionService</c>) mới nhìn thấy interface này.
    /// </summary>
    public interface ILatencySessionFactory
    {
        /// <summary>
        /// Dispose là lúc chốt sổ và đẩy báo cáo sang <see cref="ILatencyReporter"/>.
        /// Phiên lồng nhau thì phiên trong được khôi phục về phiên ngoài khi đóng.
        /// </summary>
        IDisposable Begin(string operation);
    }

    /// <summary>
    /// Nơi nhận báo cáo đã chốt. Tách khỏi phần đo để đổi cách xuất số liệu (log → Prometheus →
    /// OpenTelemetry) mà không phải đụng tới một dòng nào của các decorator (OCP).
    /// <para>
    /// BẤT BIẾN: không được ném. Cài đặt chạy trong <c>finally</c> của phiên, nên một exception ở
    /// đây sẽ nuốt mất exception thật của request.
    /// </para>
    /// </summary>
    public interface ILatencyReporter
    {
        void Report(LatencyReport report);
    }
}
