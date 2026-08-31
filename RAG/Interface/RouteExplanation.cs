namespace RAG.Interface
{
    /// <summary>
    /// Kết quả chẩn đoán định tuyến cho một câu hỏi: câu sau chuẩn hóa, điểm của MỌI route,
    /// và route thắng (nếu có).
    /// <para>
    /// Có kiểu riêng thay vì tuple để chữ ký của <see cref="IRouteDiagnostics"/> tự mô tả được.
    /// </para>
    /// </summary>
    public sealed record RouteExplanation(
        string NormalizedQuestion,
        IReadOnlyList<RouteScore> Scores,
        RouteMatch? Match);
}
