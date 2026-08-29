namespace RAG.Interface
{
    /// <summary>
    /// Node định tuyến ngữ nghĩa: so vector câu hỏi với các route "trả lời thẳng"
    /// (tán gẫu, chào hỏi, cảm ơn...) để bỏ qua toàn bộ bước truy hồi Qdrant.
    /// Route chỉ đóng vai trò bộ lọc: không route nào khớp thì mặc định vẫn là đường RAG.
    /// </summary>
    public interface ISemanticRouter
    {
        /// <summary>
        /// Trả về route khớp có điểm cao nhất, hoặc <c>null</c> nếu phải đi đường truy hồi.
        /// Đồng bộ và thuần in-memory vì vector utterance đã được nạp sẵn lúc khởi động.
        /// </summary>
        /// <param name="question">
        /// Câu hỏi đã chuẩn hóa. Chỉ dùng để kiểm tra độ dài; việc so khớp hoàn toàn dựa trên vector.
        /// </param>
        /// <param name="questionEmbedding">
        /// Vector của <paramref name="question"/>. Caller phải embedding trước và truyền vào đây,
        /// nhờ vậy mỗi request chỉ tốn đúng một lần gọi embedding cho cả định tuyến lẫn truy hồi.
        /// Mảng này KHÔNG bị sửa đổi.
        /// </param>
        RouteMatch? Route(string question, float[] questionEmbedding);

        /// <summary>
        /// Điểm của mọi route, kể cả route không khớp. Dùng cho endpoint chẩn đoán khi tinh chỉnh ngưỡng.
        /// Trả về danh sách rỗng khi node bị tắt.
        /// </summary>
        IReadOnlyList<RouteScore> Explain(string question, float[] questionEmbedding);

        /// <summary>
        /// Thêm câu mẫu vào một route đang chạy mà không cần khởi động lại ứng dụng.
        /// </summary>
        /// <param name="routeName">Tên route đích; phải là route đã khai báo trong cấu hình.</param>
        /// <param name="utterances">Các câu dạng text; sẽ được nhúng bằng provider embedding.</param>
        /// <param name="vectors">
        /// Các vector đã chuẩn bị sẵn, nạp thẳng không qua API. Phải đúng số chiều và khác vector 0.
        /// </param>
        Task<RouteUpdateResult> AddUtterancesAsync(string routeName,
                                                   IReadOnlyList<string> utterances,
                                                   IReadOnlyList<float[]> vectors,
                                                   CancellationToken cancellationToken = default);
    }
}
