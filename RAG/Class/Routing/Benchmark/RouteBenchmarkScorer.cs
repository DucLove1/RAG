using RAG.Interface;

namespace RAG.Class.Routing.Benchmark
{
    /// <summary>Tổng hợp số liệu từ kết quả từng câu. Hàm thuần, không gọi gì ra ngoài.</summary>
    public static class RouteBenchmarkScorer
    {
        public static RouteBenchmarkReport BuildReport(IReadOnlyList<RouteBenchmarkCaseResult> cases,
                                                       bool normalized,
                                                       bool onlyMistakes)
        {
            var strategies = cases
                .SelectMany(result => result.Results)
                .Select(outcome => outcome.Strategy)
                .Distinct()
                .ToList();

            var summaries = strategies.Select(strategy => Summarize(strategy, cases)).ToList();

            var listed = onlyMistakes
                ? cases.Where(result => result.Results.Any(outcome => !outcome.Correct)).ToList()
                : cases;

            return new RouteBenchmarkReport(
                cases.Count,
                normalized,
                cases.Count(result => !result.Agree),
                summaries,
                listed);
        }

        private static RouteBenchmarkStrategySummary Summarize(string strategy,
                                                               IReadOnlyList<RouteBenchmarkCaseResult> cases)
        {
            var rows = cases
                .Select(result => new Row(result, result.Results.First(outcome => outcome.Strategy == strategy)))
                .ToList();

            // Lượt lỗi bị loại khỏi độ trễ: router Jev bỏ cuộc ở mốc timeout, và để những con số đó
            // trộn vào thì trung bình đo "bao lâu thì hết hạn" chứ không phải "bao lâu thì chọn xong route".
            var latencies = rows
                .Where(row => !row.Outcome.Failed)
                .Select(row => row.Outcome.LatencyMs)
                .OrderBy(value => value)
                .ToList();
            var correct = rows.Count(row => row.Outcome.Correct);

            return new RouteBenchmarkStrategySummary(
                strategy,
                correct,
                rows.Count,
                rows.Count == 0 ? 0 : Math.Round((double)correct / rows.Count, 3),
                rows.Count(row => row.Outcome.Failed),
                latencies.Count == 0 ? 0 : Math.Round(latencies.Average(), 1),
                Math.Round(Median(latencies), 1),
                latencies.Count == 0 ? 0 : latencies[^1],
                Tally(rows, row => row.Case.ExpectedRoute),
                Tally(rows, row => row.Case.Tag));
        }

        private static IReadOnlyDictionary<string, RouteBenchmarkTally> Tally(IEnumerable<Row> rows,
                                                                               Func<Row, string> key) =>
            rows.GroupBy(key)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key,
                              group => new RouteBenchmarkTally(group.Count(row => row.Outcome.Correct), group.Count()));

        private static double Median(IReadOnlyList<double> sorted)
        {
            if (sorted.Count == 0)
                return 0;

            var middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }

        private sealed record Row(RouteBenchmarkCaseResult Case, RouteBenchmarkOutcome Outcome);
    }
}
