namespace RAG.Interface
{
    /// <summary>
    /// Thăm dò xem phần NATIVE của một provider cache chạy trong tiến trình có nạp được hay không.
    /// <para>
    /// Tồn tại vì provider FAISS cố ý fail-open: thiếu thư viện hệ thống (libopenblas, libgomp,
    /// glibc quá cũ) không làm app sập mà chỉ khiến cache KHÔNG BAO GIỜ TRÚNG — một lỗi vô hình đã
    /// thực sự xảy ra trên Render. Thăm dò một lần lúc khởi động biến nó thành một dòng log Error
    /// ngay đầu đời tiến trình.
    /// </para>
    /// <para>
    /// Tách khỏi <see cref="ISemanticAnswerCache"/> theo ISP, cùng cách <see cref="IGraphWarmup"/>
    /// tách khỏi <see cref="IGraphStore"/>: cài đặt nằm trong lớp giữ binding native để service
    /// khởi động không phải import <c>Faiss.*</c>.
    /// </para>
    /// </summary>
    public interface IAnswerCacheNativeProbe
    {
        /// <summary>Không bao giờ ném.</summary>
        /// <param name="error">Lỗi đã bắt được khi thăm dò hỏng; <c>null</c> khi thành công.</param>
        /// <returns><c>true</c> nếu tạo và hủy được một index native thật.</returns>
        bool TryProbe(out Exception? error);
    }
}
