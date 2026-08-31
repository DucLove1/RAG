namespace RAG.Interface
{
    /// <summary>
    /// Node định tuyến ngữ nghĩa: nhận diện các câu "trả lời thẳng" (tán gẫu, chào hỏi, cảm ơn...)
    /// để bỏ qua toàn bộ bước truy hồi Qdrant.
    /// Route chỉ đóng vai trò bộ lọc: không route nào khớp thì mặc định vẫn là đường RAG.
    /// </summary>
    public interface ISemanticRouter
    {
        /// <summary>
        /// Trả về route khớp, hoặc <c>null</c> nếu phải đi đường truy hồi.
        /// <para>
        /// KHÔNG nhận vector câu hỏi. Bản trước bắt caller nhúng trước rồi truyền vào, vì cài đặt
        /// duy nhất lúc đó chấm điểm bằng cosine. Nay có cài đặt hỏi thẳng LLM và không cần vector
        /// nào cả, nên tham số đó chỉ còn là chi tiết của một chiến lược rò rỉ vào hợp đồng chung
        /// (ISP). Cài đặt nào cần vector thì tự lấy qua <see cref="IEmbeddingProvider"/> — nó đã bị
        /// bọc bởi decorator cache nên lần nhúng sau ở nhánh truy hồi vẫn là một lần trúng cache,
        /// tổng chi phí không đổi.
        /// </para>
        /// <para>
        /// Nhờ vậy pipeline gọi định tuyến TRƯỚC khi nhúng: câu tán gẫu không tốn lượt gọi API
        /// embedding nào, thay vì một lượt như trước.
        /// </para>
        /// <para>
        /// Bất đồng bộ vì cài đặt bằng LLM phải gọi mạng. Mọi lỗi đều fail-open về <c>null</c>,
        /// tức là về đường RAG — giống cách <c>LlmQueryNormalizer</c> fail-open về câu hỏi gốc.
        /// </para>
        /// </summary>
        /// <param name="question">Câu hỏi đã chuẩn hóa.</param>
        Task<RouteMatch?> RouteAsync(string question, CancellationToken cancellationToken = default);
    }
}
