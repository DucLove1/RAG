using RAG.Class.Validation;
using RAG.Interface;

namespace RAG.Class
{
    /// <summary>Yêu cầu chẩn đoán định tuyến; chỉ cần câu hỏi vì không sinh câu trả lời.</summary>
    public record RouteDebugRequest([NotBlank] string Question);

    /// <summary>
    /// Kết quả chẩn đoán: đánh giá của mọi route, để tinh chỉnh mà không phải đọc log.
    /// </summary>
    /// <param name="Strategy">
    /// Chiến lược đã đưa ra quyết định. Không có trường này thì một phản hồi toàn <c>score: null</c>
    /// không phân biệt được "router LLM đang chạy" với "router embedding vừa hỏng".
    /// </param>
    /// <param name="MatchedScore">Điểm của route thắng; <c>null</c> khi chiến lược không chấm điểm.</param>
    public record RouteDebugResponse(
        string Question,
        string NormalizedQuestion,
        string Strategy,
        string? MatchedRoute,
        double? MatchedScore,
        IReadOnlyList<RouteScore> Scores);

    /// <summary>
    /// Yêu cầu thêm câu mẫu vào một route đang chạy.
    /// Có thể gửi câu dạng text (sẽ được nhúng), vector đã có sẵn, hoặc cả hai.
    /// </summary>
    /// <param name="Route">Tên route đích, ví dụ "chitchat".</param>
    /// <param name="Utterances">Các câu dạng text cần nhúng.</param>
    /// <param name="Vectors">Các vector đã chuẩn bị sẵn, phải đúng số chiều của model.</param>
    public record AddUtterancesRequest(
        [NotBlank] string Route,
        List<string>? Utterances,
        List<float[]>? Vectors);

    /// <summary>
    /// Phản hồi cho lần thêm câu mẫu. Giữ nguyên hình dạng JSON như trước (success, message,
    /// added, skipped, totalInRoute, persisted) để client không phải sửa gì.
    /// </summary>
    public record AddUtterancesResponse(
        bool Success,
        string Message,
        int Added,
        int Skipped,
        int TotalInRoute,
        bool Persisted);
}
