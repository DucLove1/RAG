using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;
using System.Diagnostics;

namespace RAG.Class.Routing.Benchmark
{
    /// <summary>
    /// Chạy từng câu qua mọi chiến lược trong cấu hình.
    /// <para>
    /// Câu được chuẩn hóa MỘT lần rồi đưa CÙNG một chuỗi cho mọi chiến lược, để so sánh là so
    /// router chứ không so độ ngẫu nhiên của bước chuẩn hóa. Gọi qua <see cref="IRouteExplainer"/>
    /// nên không đi qua cache quyết định định tuyến. Chạy tuần tự để không đụng rate limit.
    /// </para>
    /// </summary>
    public sealed class RouteBenchmarkRunner : IRouteBenchmark
    {
        private readonly IRouteBenchmarkCaseSource _caseSource;
        private readonly IRouteExplainerResolver _explainerResolver;
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly RouteBenchmarkConfig _config;

        public RouteBenchmarkRunner(IRouteBenchmarkCaseSource caseSource,
                                    IRouteExplainerResolver explainerResolver,
                                    IQueryNormalizer queryNormalizer,
                                    IOptions<RouteBenchmarkConfig> options)
        {
            _caseSource = caseSource;
            _explainerResolver = explainerResolver;
            _queryNormalizer = queryNormalizer;
            _config = options.Value;
        }

        public async Task<RouteBenchmarkReport> RunAsync(bool onlyMistakes, CancellationToken cancellationToken = default)
        {
            var cases = _caseSource.Load();
            var results = new List<RouteBenchmarkCaseResult>(cases.Count);

            await WarmUpAsync(cancellationToken);

            for (var index = 0; index < cases.Count; index++)
            {
                var benchmarkCase = cases[index];

                var normalized = _config.NormalizeQuestions
                    ? await _queryNormalizer.NormalizeAsync(benchmarkCase.Question, ConversationHistory.Empty, cancellationToken)
                    : benchmarkCase.Question;

                var outcomes = new List<RouteBenchmarkOutcome>(_config.Strategies.Count);

                foreach (var strategy in OrderFor(index))
                {
                    outcomes.Add(await RunOneAsync(strategy, normalized, benchmarkCase.ExpectedRoute, cancellationToken));

                    if (_config.DelayBetweenCallsMilliseconds > 0)
                        await Task.Delay(_config.DelayBetweenCallsMilliseconds, cancellationToken);
                }

                // Gọi theo thứ tự xoay vòng nhưng LƯU theo thứ tự cấu hình, để cột JSON/CSV ổn định.
                outcomes = outcomes
                    .OrderBy(outcome => _config.Strategies.FindIndex(strategy => strategy.ToString() == outcome.Strategy))
                    .ToList();

                var agree = outcomes
                    .Select(outcome => outcome.ActualRoute)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() <= 1;

                results.Add(new RouteBenchmarkCaseResult(
                    benchmarkCase.Id,
                    benchmarkCase.Question,
                    normalized,
                    benchmarkCase.ExpectedRoute,
                    benchmarkCase.Tag,
                    benchmarkCase.Note,
                    agree,
                    outcomes));
            }

            return RouteBenchmarkScorer.BuildReport(results, _config.NormalizeQuestions, onlyMistakes);
        }

        /// <summary>
        /// Gọi thử một lần cho mỗi chiến lược, bỏ kết quả: lượt đầu gánh handshake và dựng client,
        /// không được tính vào số đo của câu đầu bộ benchmark.
        /// </summary>
        private async Task WarmUpAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_config.WarmupQuestion))
                return;

            foreach (var strategy in _config.Strategies.Distinct())
                await _explainerResolver.Resolve(strategy).ExplainAsync(_config.WarmupQuestion, cancellationToken);
        }

        /// <summary>Câu thứ <paramref name="index"/> chạy danh sách chiến lược xoay vòng <paramref name="index"/> bước.</summary>
        private IEnumerable<Class.Constants.SemanticRouterStrategy> OrderFor(int index)
        {
            var strategies = _config.Strategies;

            if (!_config.AlternateOrder || strategies.Count < 2)
                return strategies;

            var shift = index % strategies.Count;
            return strategies.Skip(shift).Concat(strategies.Take(shift));
        }

        private async Task<RouteBenchmarkOutcome> RunOneAsync(Class.Constants.SemanticRouterStrategy strategy,
                                                              string normalizedQuestion,
                                                              string expectedRoute,
                                                              CancellationToken cancellationToken)
        {
            var explainer = _explainerResolver.Resolve(strategy);

            var started = Stopwatch.GetTimestamp();
            var explanation = await explainer.ExplainAsync(normalizedQuestion, cancellationToken);
            var latency = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // Router trả Match = null nghĩa là "đi đường RAG": đó cũng là một quyết định, nên đổi
            // thành nhãn RAG trong cấu hình để chấm như mọi route khác.
            var actual = explanation.Match?.Name ?? _config.NoRouteLabel;

            // Route điểm cao nhất kể cả khi không khớp — tách được ca "chọn đúng nhưng dưới ngưỡng".
            var top = explanation.Scores
                .Where(score => score.Score is not null)
                .MaxBy(score => score.Score);

            return new RouteBenchmarkOutcome(
                strategy.ToString(),
                actual,
                explanation.Match?.Score,
                top?.Name,
                top?.Score,
                !explanation.Failed && string.Equals(actual, expectedRoute, StringComparison.OrdinalIgnoreCase),
                Math.Round(latency, 1),
                explanation.Failed);
        }
    }
}
