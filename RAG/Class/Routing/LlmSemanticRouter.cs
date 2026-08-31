using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Định tuyến bằng cách để LLM đọc câu của người chơi và chọn đúng một nhãn route.
    /// <para>
    /// Thay cho cách chấm điểm cosine, cách này đọc HIỂU câu hỏi, nên xử lý được thứ mà vector
    /// không phân biệt nổi: câu pha trộn ý định ("chào ông, cho tôi hỏi giá kiếm sắt" vừa giống
    /// route chào hỏi vừa là câu hỏi tri thức). Router embedding phải né loại câu đó bằng cửa chặn
    /// độ dài; ở đây nó là một luật tường minh trong prompt.
    /// </para>
    /// <para>
    /// Không cần vector nào, nên khi chạy chiến lược này, câu tán gẫu không tốn lượt gọi API
    /// embedding nào cả — pipeline định tuyến trước rồi mới nhúng.
    /// </para>
    /// <para>
    /// Fail-open như <c>LlmQueryNormalizer</c>: lỗi mạng, hết hạn mức key, hay đầu ra không đọc
    /// được đều trả <c>null</c>, tức là về đường RAG. Đường RAG luôn trả lời được câu tán gẫu
    /// (chỉ kém tự nhiên hơn), nên nhầm theo hướng này là nhầm rẻ.
    /// </para>
    /// <para>
    /// Danh mục route, system prompt và từ điển tra nhãn đều dựng MỘT lần trong constructor. Điều
    /// đó hợp lệ vì <see cref="SemanticRouterConfig"/> cố tình bind qua <c>IOptions</c> chứ không
    /// phải <c>IOptionsMonitor</c> — bảng route không đổi trong suốt vòng đời tiến trình.
    /// </para>
    /// </summary>
    public sealed class LlmSemanticRouter : ISemanticRouter, IRouteExplainer
    {
        private readonly ILLMProvider _llmProvider;
        private readonly LlmRouterConfig _config;
        private readonly ILogger<LlmSemanticRouter> _logger;

        /// <summary>Tên route → kết quả định tuyến dựng sẵn; mỗi request chỉ còn một phép tra.</summary>
        private readonly IReadOnlyDictionary<string, RouteMatch> _matches;

        private readonly IReadOnlyList<string> _routeNames;
        private readonly RouteLabelParser _parser;
        private readonly string _systemPrompt;

        public LlmSemanticRouter(ILlmProviderResolver llmProviderResolver,
                                 IOptions<SemanticRouterConfig> options,
                                 IOptions<PromptConfig> promptOptions,
                                 ILogger<LlmSemanticRouter> logger)
        {
            var routerConfig = options.Value;
            _config = routerConfig.Llm;
            _logger = logger;

            // Resolve theo khóa cấu hình riêng, độc lập với provider của đường trả lời — giống
            // cách node chuẩn hóa chọn provider của nó.
            _llmProvider = llmProviderResolver.Resolve(_config.Provider);

            var routes = RouteTableFactory.Resolve(routerConfig, promptOptions.Value, logger);

            _matches = routes.ToDictionary(
                route => route.Name,
                route => new RouteMatch(route.Name, Score: null, route.SystemPromptTemplate, route.UserPromptTemplate),
                StringComparer.Ordinal);

            _routeNames = routes.Select(route => route.Name).ToList();
            _parser = new RouteLabelParser(_routeNames, _config.NoMatchLabel);
            _systemPrompt = _config.BuildSystemPrompt(BuildRouteCatalog(routes));

            if (_matches.Count == 0)
                _logger.LogWarning("Không có route nào dùng được; mọi câu hỏi sẽ đi đường RAG.");
        }

        public async Task<RouteMatch?> RouteAsync(string question, CancellationToken cancellationToken = default)
        {
            var resolution = await ClassifyAsync(question, cancellationToken);

            if (resolution.Outcome != RouteLabelOutcome.Matched)
                return null;

            if (!_matches.TryGetValue(resolution.RouteName, out var match))
                return null;

            _logger.LogDebug("Định tuyến khớp route {Route}, bỏ qua truy hồi.", match.Name);

            return match;
        }

        public async Task<RouteExplanation> ExplainAsync(string normalizedQuestion,
                                                         CancellationToken cancellationToken = default)
        {
            var resolution = await ClassifyAsync(normalizedQuestion, cancellationToken);

            var matchedName = resolution.Outcome == RouteLabelOutcome.Matched ? resolution.RouteName : null;

            // Điểm và ngưỡng đều null: chiến lược này chọn nhãn chứ không chấm điểm. Trường Strategy
            // trong phản hồi là thứ cho người vận hành biết đó là bình thường chứ không phải hỏng.
            var scores = _routeNames
                .Select(name => new RouteScore(name, Score: null, Threshold: null,
                    Matched: string.Equals(name, matchedName, StringComparison.Ordinal)))
                .ToList();

            var match = matchedName is not null && _matches.TryGetValue(matchedName, out var found) ? found : null;

            return new RouteExplanation(normalizedQuestion, scores, match, SemanticRouterStrategy.Llm);
        }

        /// <summary>
        /// Một lượt gọi LLM để phân loại. Mọi lỗi đều thành <see cref="RouteLabelOutcome.Unparseable"/>,
        /// tức là fail-open về đường RAG.
        /// </summary>
        private async Task<RouteLabelResolution> ClassifyAsync(string question, CancellationToken cancellationToken)
        {
            if (!ShouldClassify(question))
                return new RouteLabelResolution(RouteLabelOutcome.Unparseable, string.Empty);

            try
            {
                var output = await _llmProvider.AskAsync(
                    _systemPrompt,
                    _config.BuildUserPrompt(question),
                    _config.Model,
                    cancellationToken);

                var resolution = _parser.Resolve(output);

                if (resolution.Outcome == RouteLabelOutcome.Unparseable)
                {
                    _logger.LogWarning("Không đọc được nhãn route từ đầu ra của LLM: \"{Output}\". Dùng đường RAG.",
                        output);
                }

                return resolution;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phân loại định tuyến thất bại, dùng đường RAG.");
                return new RouteLabelResolution(RouteLabelOutcome.Unparseable, string.Empty);
            }
        }

        /// <summary>
        /// Cửa chặn RIÊNG của chiến lược này, không phải bản sao của <c>Embedding:MaxRoutableLength</c>.
        /// Ngưỡng ở đây nới rộng hơn nhiều vì nó chỉ nhằm chặn đoạn văn dài — ranh giới câu pha trộn
        /// ý định do luật trong prompt xử lý, và đó chính là lý do đổi sang chiến lược này.
        /// </summary>
        private bool ShouldClassify(string question) =>
            _matches.Count > 0 &&
            !string.IsNullOrWhiteSpace(question) &&
            question.Length <= _config.MaxInputLength;

        /// <summary>
        /// Dựng danh mục nhãn cho system prompt: mỗi route một dòng gồm tên, mô tả và vài câu mẫu
        /// làm ví dụ few-shot. Lấy các câu ĐẦU danh sách, nên thứ tự trong cấu hình có ý nghĩa.
        /// </summary>
        private string BuildRouteCatalog(IReadOnlyList<ResolvedRoute> routes)
        {
            var blocks = routes.Select(route => string.Format(
                _config.RouteBlockTemplate,
                route.Name,
                route.Description,
                string.Join(_config.ExampleSeparator, route.Utterances.Take(Math.Max(1, _config.MaxExamplesPerRoute)))));

            return string.Join(_config.RouteBlockSeparator, blocks);
        }
    }
}
