using RAG.Class.Constants;

namespace RAG.Interface
{
    /// <summary>
    /// Kết quả chẩn đoán định tuyến cho một câu hỏi: câu sau chuẩn hóa, đánh giá của MỌI route,
    /// và route thắng (nếu có).
    /// <para>
    /// Có kiểu riêng thay vì tuple để chữ ký của <see cref="IRouteExplainer"/> tự mô tả được.
    /// </para>
    /// </summary>
    /// <param name="Strategy">
    /// Chiến lược nào đã đưa ra quyết định. Không có trường này thì người vận hành đọc một phản hồi
    /// toàn <c>score: null</c> sẽ không biết là router LLM đang chạy hay router embedding vừa hỏng.
    /// </param>
    /// <param name="Failed">
    /// API phân loại không trả về đáp án dùng được trong thời hạn (lỗi mạng, quá hạn, hết key,
    /// phản hồi hỏng). Khi đó <paramref name="Match"/> là null vì fail-open chứ không phải vì router
    /// QUYẾT ĐỊNH đi RAG — benchmark cần phân biệt hai trường hợp này.
    /// </param>
    public sealed record RouteExplanation(
        string NormalizedQuestion,
        IReadOnlyList<RouteScore> Scores,
        RouteMatch? Match,
        SemanticRouterStrategy Strategy,
        bool Failed = false);
}
