using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Định tuyến bằng mô hình quyết định Jev (TypeSafe System One): một câu hỏi Choice, mỗi route
    /// là một option, cộng một option "không khớp" đứng ĐẦU danh sách.
    /// <para>
    /// Khác router LLM ở chỗ đáp án là JSON có kiểu kèm xác suất từng option, nên không cần parse
    /// văn bản tự do và có một con số để đặt ngưỡng. Khác router embedding ở chỗ mô hình đọc được
    /// mô tả route, nên không cần nạp vector câu mẫu lúc khởi động.
    /// </para>
    /// <para>
    /// Fail-open như hai chiến lược kia: lỗi mạng, quá hạn, hết key, phản hồi hỏng, hay xác suất
    /// dưới ngưỡng đều trả <c>null</c>, tức là về đường RAG.
    /// </para>
    /// <para>
    /// Câu hỏi Choice và từ điển tra route đều dựng MỘT lần trong constructor — hợp lệ vì
    /// <see cref="SemanticRouterConfig"/> cố tình bind qua <c>IOptions</c>.
    /// </para>
    /// </summary>
    public sealed class JevSemanticRouter : ISemanticRouter, IRouteExplainer
    {
        private readonly IJevClient _jevClient;
        private readonly JevRouterConfig _config;
        private readonly ILogger<JevSemanticRouter> _logger;

        /// <summary>Tên route → kết quả định tuyến dựng sẵn (chưa có điểm).</summary>
        private readonly IReadOnlyDictionary<string, RouteMatch> _matches;

        private readonly IReadOnlyList<string> _routeNames;
        private readonly SystemOneQuestion _question;

        public JevSemanticRouter(IJevClient jevClient,
                                 IOptions<SemanticRouterConfig> options,
                                 IOptions<PromptConfig> promptOptions,
                                 ILogger<JevSemanticRouter> logger)
        {
            var routerConfig = options.Value;
            _jevClient = jevClient;
            _config = routerConfig.Jev;
            _logger = logger;

            var routes = RouteTableFactory.Resolve(routerConfig, promptOptions.Value, logger);

            _matches = routes.ToDictionary(
                route => route.Name,
                route => new RouteMatch(route.Name, Score: null, route.SystemPromptTemplate, route.UserPromptTemplate, route.FixedReply),
                StringComparer.Ordinal);

            _routeNames = routes.Select(route => route.Name).ToList();
            _question = BuildQuestion(routes);

            if (_matches.Count == 0)
                _logger.LogWarning("Không có route nào dùng được; mọi câu hỏi sẽ đi đường RAG.");
        }

        public async Task<RouteMatch?> RouteAsync(string question, CancellationToken cancellationToken = default)
        {
            var (answer, _) = await ClassifyAsync(question, cancellationToken);
            var match = Resolve(answer);

            if (match is not null)
            {
                _logger.LogDebug("Định tuyến khớp route {Route} (p={Score:F3}), bỏ qua truy hồi.",
                    match.Name, match.Score);
            }

            return match;
        }

        public async Task<RouteExplanation> ExplainAsync(string normalizedQuestion,
                                                         CancellationToken cancellationToken = default)
        {
            var (answer, failed) = await ClassifyAsync(normalizedQuestion, cancellationToken);
            var match = Resolve(answer);

            // Chấm một lần rồi suy ra cả bảng điểm lẫn route thắng từ cùng một đáp án, để điểm và
            // kết luận luôn khớp nhau. Không gọi được Jev thì điểm là null — giống chiến lược LLM.
            var scores = _routeNames
                .Select(name => new RouteScore(
                    name,
                    Score: ProbabilityOf(answer, name),
                    Threshold: _config.MinProbability,
                    Matched: string.Equals(name, match?.Name, StringComparison.Ordinal)))
                .OrderByDescending(score => score.Score ?? double.MinValue)
                .ToList();

            return new RouteExplanation(normalizedQuestion, scores, match, SemanticRouterStrategy.Jev, failed);
        }

        /// <summary>
        /// Một lượt gọi Jev. Mọi lỗi (trừ caller hủy) đều thành đáp án <c>null</c>, tức là fail-open;
        /// <c>Failed</c> tách lượt lỗi khỏi lượt bị cửa chặn bỏ qua — cả hai đều không có đáp án.
        /// </summary>
        private async Task<(SystemOneAnswer? Answer, bool Failed)> ClassifyAsync(string question,
                                                                                 CancellationToken cancellationToken)
        {
            if (!ShouldClassify(question))
                return (null, false);

            try
            {
                return (await _jevClient.AskChoiceAsync(question, _config.QuestionId, _question, cancellationToken), false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phân loại định tuyến bằng Jev thất bại, dùng đường RAG.");
                return (null, true);
            }
        }

        /// <summary>
        /// Chỉ khớp khi Jev chọn một route có thật (không phải option "không khớp") VÀ xác suất của
        /// nó đạt ngưỡng. Nhãn lạ không có trong bảng route cũng đi RAG.
        /// </summary>
        private RouteMatch? Resolve(SystemOneAnswer? answer)
        {
            if (answer?.Choice is null)
                return null;

            if (string.Equals(answer.Choice, _config.NoMatchLabel, StringComparison.Ordinal))
                return null;

            if (!_matches.TryGetValue(answer.Choice, out var match))
            {
                _logger.LogWarning("Jev trả về nhãn không có trong bảng route: \"{Choice}\". Dùng đường RAG.",
                    answer.Choice);
                return null;
            }

            var probability = ProbabilityOf(answer, answer.Choice);

            if (probability is null || probability < _config.MinProbability)
            {
                _logger.LogDebug("Jev chọn {Route} nhưng p={Score:F3} dưới ngưỡng {Threshold:F3}, dùng đường RAG.",
                    answer.Choice, probability, _config.MinProbability);
                return null;
            }

            return match with { Score = probability };
        }

        private static double? ProbabilityOf(SystemOneAnswer? answer, string option) =>
            answer?.Probabilities is not null && answer.Probabilities.TryGetValue(option, out var probability)
                ? probability
                : null;

        /// <summary>Cửa chặn riêng của chiến lược này: chỉ nhằm chặn đoạn văn dài.</summary>
        private bool ShouldClassify(string question) =>
            _matches.Count > 0 &&
            !string.IsNullOrWhiteSpace(question) &&
            question.Length <= _config.MaxInputLength;

        /// <summary>
        /// Dựng câu hỏi Choice. Option "không khớp" đứng ĐẦU: jev-1.13 nghiêng về option đứng trước,
        /// nên đặt đường an toàn ở đó để thiên lệch rơi về phía RAG.
        /// </summary>
        private SystemOneQuestion BuildQuestion(IReadOnlyList<ResolvedRoute> routes)
        {
            var criteria = new OrderedDictionary<string, object>(StringComparer.Ordinal)
            {
                [_config.NoMatchLabel] = _config.NoMatchDescription
            };

            foreach (var route in routes)
            {
                var examples = route.Utterances.Take(_config.MaxExamplesPerRoute).ToList();

                // Route thiếu mô tả vẫn phải có "what" không rỗng; tên route là thứ tốt nhất còn lại.
                criteria[route.Name] = new SystemOneCriterion
                {
                    What = string.IsNullOrWhiteSpace(route.Description) ? route.Name : route.Description,
                    Examples = examples.Count > 0 ? examples : null
                };
            }

            return new SystemOneQuestion
            {
                Type = TypeSafeApiDefaults.ChoiceQuestionType,
                Instructions = _config.Instructions,
                Criteria = criteria
            };
        }
    }
}
