using RAG.Class.Constants;

namespace RAG.Interface
{
    /// <summary>
    /// Chạy bộ câu hỏi cố định qua nhiều chiến lược định tuyến và chấm điểm từng chiến lược.
    /// Chỉ chạy bước định tuyến: không truy hồi, không sinh câu trả lời.
    /// </summary>
    public interface IRouteBenchmark
    {
        /// <param name="onlyMistakes">true thì danh sách case chỉ giữ những câu có ít nhất một chiến lược chọn sai.</param>
        Task<RouteBenchmarkReport> RunAsync(bool onlyMistakes, CancellationToken cancellationToken = default);
    }

    /// <summary>Nguồn bộ câu hỏi của benchmark.</summary>
    public interface IRouteBenchmarkCaseSource
    {
        IReadOnlyList<RouteBenchmarkCase> Load();
    }

    /// <summary>Lấy <see cref="IRouteExplainer"/> theo chiến lược mà không để consumer chạm IServiceProvider.</summary>
    public interface IRouteExplainerResolver
    {
        IRouteExplainer Resolve(SemanticRouterStrategy strategy);
    }

    /// <summary>Một câu trong bộ benchmark kèm route kỳ vọng.</summary>
    public sealed record RouteBenchmarkCase(string Id, string Question, string ExpectedRoute, string Tag, string Note);

    /// <summary>Quyết định của một chiến lược cho một câu.</summary>
    /// <param name="ActualRoute">Route được chọn; không route nào khớp thì là nhãn RAG trong cấu hình.</param>
    /// <param name="TopCandidate">
    /// Route điểm cao nhất kể cả khi KHÔNG khớp (vd. Jev chọn chitchat nhưng xác suất dưới ngưỡng).
    /// Luôn null với chiến lược không chấm điểm (Llm).
    /// </param>
    /// <param name="Correct">Luôn false khi <paramref name="Failed"/>: lỗi API không được tính là đoán trúng nhãn RAG.</param>
    /// <param name="Failed">API lỗi / quá hạn, router fail-open. Lượt này bị loại khỏi thống kê độ trễ.</param>
    public sealed record RouteBenchmarkOutcome(
        string Strategy,
        string ActualRoute,
        double? Score,
        string? TopCandidate,
        double? TopCandidateScore,
        bool Correct,
        double LatencyMs,
        bool Failed);

    public sealed record RouteBenchmarkCaseResult(
        string Id,
        string Question,
        string NormalizedQuestion,
        string ExpectedRoute,
        string Tag,
        string Note,
        bool Agree,
        IReadOnlyList<RouteBenchmarkOutcome> Results);

    public sealed record RouteBenchmarkTally(int Correct, int Total);

    /// <param name="Failures">Số lượt API lỗi / quá hạn. Avg/Median/Max chỉ tính trên các lượt còn lại.</param>
    public sealed record RouteBenchmarkStrategySummary(
        string Strategy,
        int Correct,
        int Total,
        double Accuracy,
        int Failures,
        double AvgLatencyMs,
        double MedianLatencyMs,
        double MaxLatencyMs,
        IReadOnlyDictionary<string, RouteBenchmarkTally> ByExpectedRoute,
        IReadOnlyDictionary<string, RouteBenchmarkTally> ByTag);

    public sealed record RouteBenchmarkReport(
        int TotalCases,
        bool Normalized,
        int Disagreements,
        IReadOnlyList<RouteBenchmarkStrategySummary> Summaries,
        IReadOnlyList<RouteBenchmarkCaseResult> Cases);

    /// <summary>Gọi benchmark khi nó đang tắt hoặc chạy ngoài Development. Ánh xạ thành 404.</summary>
    public sealed class RouteBenchmarkDisabledException : Exception
    {
        public RouteBenchmarkDisabledException(string message) : base(message) { }
    }
}
