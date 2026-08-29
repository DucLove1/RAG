using RAG.Interface;

namespace RAG.Class
{
    /// <summary>Yêu cầu chẩn đoán định tuyến; chỉ cần câu hỏi vì không sinh câu trả lời.</summary>
    public record RouteDebugRequest(string Question);

    /// <summary>
    /// Kết quả chẩn đoán: điểm của mọi route kèm ngưỡng tương ứng, để tinh chỉnh ngưỡng
    /// mà không phải đọc log.
    /// </summary>
    public record RouteDebugResponse(
        string Question,
        string NormalizedQuestion,
        string? MatchedRoute,
        IReadOnlyList<RouteScore> Scores);

    /// <summary>
    /// Yêu cầu thêm câu mẫu vào một route đang chạy.
    /// Có thể gửi câu dạng text (sẽ được nhúng), vector đã có sẵn, hoặc cả hai.
    /// </summary>
    /// <param name="Route">Tên route đích, ví dụ "chitchat".</param>
    /// <param name="Utterances">Các câu dạng text cần nhúng.</param>
    /// <param name="Vectors">Các vector đã chuẩn bị sẵn, phải đúng số chiều của model.</param>
    public record AddUtterancesRequest(
        string Route,
        List<string>? Utterances,
        List<float[]>? Vectors);
}
