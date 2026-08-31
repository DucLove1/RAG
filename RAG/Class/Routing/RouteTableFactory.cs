using RAG.Class.Config;

namespace RAG.Class.Routing
{
    /// <summary>Một route đã qua kiểm tra và đã giải xong template, chưa gắn cách nhận diện nào.</summary>
    /// <param name="Threshold">Ngưỡng cosine đã giải; chiến lược không chấm điểm thì bỏ qua.</param>
    public sealed record ResolvedRoute(
        string Name,
        string Description,
        double Threshold,
        string SystemPromptTemplate,
        string UserPromptTemplate,
        IReadOnlyList<string> Utterances);

    /// <summary>
    /// Luật "route nào dùng được, và prompt của nó là gì" — dùng chung cho mọi chiến lược định tuyến.
    /// <para>
    /// Tồn tại vì cả hai chiến lược đều phải trả lời đúng những câu hỏi này: bỏ route thiếu Name
    /// hay thiếu UserPromptTemplate, cảnh báo khi template thiếu <c>{0}</c>, và lùi về
    /// <c>Prompts:AnswerSystemTemplate</c> khi route không tự khai system prompt. Để mỗi router
    /// giữ một bản riêng là cách chắc chắn nhất để hai bản lệch nhau sau vài lần sửa — và không
    /// có test nào bắt được điều đó.
    /// </para>
    /// </summary>
    public static class RouteTableFactory
    {
        public static IReadOnlyList<ResolvedRoute> Resolve(SemanticRouterConfig config,
                                                           PromptConfig promptConfig,
                                                           ILogger logger)
        {
            var resolved = new List<ResolvedRoute>(config.Routes.Count);

            foreach (var route in config.Routes)
            {
                if (string.IsNullOrWhiteSpace(route.Name) || string.IsNullOrWhiteSpace(route.UserPromptTemplate))
                {
                    logger.LogWarning("Bỏ qua route thiếu Name hoặc UserPromptTemplate.");
                    continue;
                }

                if (!route.UserPromptTemplate.Contains("{0}", StringComparison.Ordinal))
                {
                    logger.LogWarning("UserPromptTemplate của route {Route} không chứa {{0}}, câu hỏi sẽ bị bỏ khỏi prompt.",
                        route.Name);
                }

                // Template mặc định có câu "không có ngữ cảnh thì trả lời không biết", vốn phản tác
                // dụng trên nhánh trả lời thẳng — nhưng vẫn tốt hơn là không có system prompt nào.
                var systemTemplate = string.IsNullOrWhiteSpace(route.SystemPromptTemplate)
                    ? promptConfig.AnswerSystemTemplate
                    : route.SystemPromptTemplate;

                var utterances = route.Utterances
                    .Where(utterance => !string.IsNullOrWhiteSpace(utterance))
                    .ToList();

                resolved.Add(new ResolvedRoute(
                    route.Name,
                    route.Description,
                    route.SimilarityThreshold ?? config.Embedding.SimilarityThreshold,
                    systemTemplate,
                    route.UserPromptTemplate,
                    utterances));
            }

            return resolved;
        }
    }
}
