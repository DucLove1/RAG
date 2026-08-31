using RAG.Class.Caching;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Routing;
using RAG.Interface;
using System.Text;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký node định tuyến ngữ nghĩa theo chiến lược được chọn trong cấu hình.</summary>
    public static class SemanticRouterServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký node định tuyến. Chiến lược nào chạy là do <c>SemanticRouter:Strategy</c> quyết
        /// định; khi tắt — hoặc không khai báo route hợp lệ nào — sẽ dùng Null Object thay vì rải
        /// if trong pipeline.
        /// </summary>
        public static IServiceCollection AddSemanticRouter(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(SemanticRouterConfig.SectionName);

            ThrowIfObsoleteKeysPresent(section);

            services.AddValidatedOptions<SemanticRouterConfig>(configuration, SemanticRouterConfig.SectionName);
            services.Configure<RouteMessagesConfig>(configuration.GetSection(RouteMessagesConfig.SectionName));

            var config = section.Get<SemanticRouterConfig>() ?? new SemanticRouterConfig();
            var strategy = ResolveStrategy(config);

            // Null Object luôn có mặt dưới khóa Off: đăng ký MỘT instance rồi trỏ cả hai vai trò
            // vào đó, vì mọi chiến lược đều cài cả ISemanticRouter lẫn IRouteExplainer.
            services.AddSingleton<PassthroughSemanticRouter>();
            services.AddKeyedSingleton<ISemanticRouter>(SemanticRouterStrategy.Off,
                (sp, _) => sp.GetRequiredService<PassthroughSemanticRouter>());
            services.AddKeyedSingleton<IRouteExplainer>(SemanticRouterStrategy.Off,
                (sp, _) => sp.GetRequiredService<PassthroughSemanticRouter>());

            switch (strategy)
            {
                case SemanticRouterStrategy.Embedding:
                    services.AddEmbeddingRouter(configuration, config);
                    break;

                case SemanticRouterStrategy.Llm:
                    services.AddLlmRouter();
                    break;

                default:
                    services.AddSingleton<IRouteUtteranceAdmin, DisabledRouteUtteranceAdmin>();
                    break;
            }

            // CHỈ đăng ký phần phụ thuộc của chiến lược đang chọn. Đăng ký sẵn cả ba để "đổi cho
            // nhanh" sẽ kéo theo warm-up của router embedding chạy nền và đốt hạn mức Gemini ngay
            // cả khi đang chạy chiến lược LLM.
            services.AddSingleton<ISemanticRouter>(sp => sp.GetRequiredKeyedService<ISemanticRouter>(strategy));
            services.AddSingleton<IRouteExplainer>(sp => sp.GetRequiredKeyedService<IRouteExplainer>(strategy));

            return services;
        }

        /// <summary>
        /// Chiến lược định tuyến bằng LLM: một lượt gọi mô hình chọn nhãn, bọc bởi cache quyết định.
        /// </summary>
        private static IServiceCollection AddLlmRouter(this IServiceCollection services)
        {
            // Đăng ký MỘT lần dưới kiểu cụ thể rồi trỏ hai vai trò vào đó qua factory. Đăng ký riêng
            // lẻ hai lần thì container tạo HAI instance — và route-debug sẽ chẩn đoán một router mà
            // đường trả lời không hề dùng, đúng cái bẫy đã ghi trong comment ở AddQueryCache.
            services.AddSingleton<LlmSemanticRouter>();

            services.AddKeyedSingleton<ISemanticRouter>(SemanticRouterStrategy.Llm, (sp, _) =>
                new CachingSemanticRouter(
                    sp.GetRequiredService<LlmSemanticRouter>(),
                    sp.GetRequiredService<IRouteDecisionCache>(),
                    sp.GetRequiredService<ILogger<CachingSemanticRouter>>()));

            // Đường chẩn đoán CỐ TÌNH không qua cache: tinh chỉnh prompt xong mà route-debug vẫn
            // trả lời từ cache thì nó không còn là công cụ chẩn đoán nữa.
            services.AddKeyedSingleton<IRouteExplainer>(SemanticRouterStrategy.Llm,
                (sp, _) => sp.GetRequiredService<LlmSemanticRouter>());

            // Chiến lược này không nhận diện route bằng vector, nên câu mẫu thêm lúc chạy không đi
            // tới đâu. Trả về mã NotSupported thay vì im lặng nhận rồi bỏ qua.
            services.AddSingleton<IRouteUtteranceAdmin, UnsupportedRouteUtteranceAdmin>();

            return services;
        }

        /// <summary>
        /// Chiến lược định tuyến bằng cosine similarity: cần warm-up vector, cache vector và kho
        /// câu mẫu thêm lúc chạy.
        /// </summary>
        private static IServiceCollection AddEmbeddingRouter(this IServiceCollection services,
                                                             IConfiguration configuration,
                                                             SemanticRouterConfig config)
        {
            WarnIfQueryCacheDisabled(services, configuration);

            if (string.IsNullOrWhiteSpace(config.Embedding.VectorCachePath))
                services.AddSingleton<IRouteVectorCache, NullRouteVectorCache>();
            else
                services.AddSingleton<IRouteVectorCache, FileRouteVectorCache>();

            if (string.IsNullOrWhiteSpace(config.Embedding.UtteranceStorePath))
                services.AddSingleton<IRouteUtteranceStore, NullRouteUtteranceStore>();
            else
                services.AddSingleton<IRouteUtteranceStore, FileRouteUtteranceStore>();

            // Trạng thái dùng chung của node. Cả ba lớp cộng tác phải thấy CÙNG một instance,
            // nếu không warm-up sẽ làm ấm một bảng route mà router không hề dùng.
            services.AddSingleton<RouteCatalog>();
            services.AddSingleton<IRouteScorer, MaxSimilarityScorer>();

            services.AddSingleton<RouteUtteranceAdmin>();
            services.AddSingleton<IRouteUtteranceAdmin>(sp => sp.GetRequiredService<RouteUtteranceAdmin>());

            services.AddSingleton<EmbeddingSemanticRouter>();
            services.AddKeyedSingleton<ISemanticRouter>(SemanticRouterStrategy.Embedding,
                (sp, _) => sp.GetRequiredService<EmbeddingSemanticRouter>());
            services.AddKeyedSingleton<IRouteExplainer>(SemanticRouterStrategy.Embedding,
                (sp, _) => sp.GetRequiredService<EmbeddingSemanticRouter>());

            services.AddSingleton<IRouterWarmup, RouteCatalogBuilder>();
            services.AddHostedService<SemanticRouterWarmupService>();

            return services;
        }

        /// <summary>
        /// Bật nhưng không có route nào dùng được thì coi như tắt: tránh warm-up vô nghĩa và tránh
        /// gọi LLM phân loại với một danh mục nhãn rỗng ở mọi request.
        /// </summary>
        private static SemanticRouterStrategy ResolveStrategy(SemanticRouterConfig config)
        {
            if (config.Strategy == SemanticRouterStrategy.Off)
                return SemanticRouterStrategy.Off;

            var hasUsableRoute = config.Routes.Any(route =>
                !string.IsNullOrWhiteSpace(route.Name) &&
                !string.IsNullOrWhiteSpace(route.UserPromptTemplate) &&
                route.Utterances.Any(utterance => !string.IsNullOrWhiteSpace(utterance)));

            return hasUsableRoute ? config.Strategy : SemanticRouterStrategy.Off;
        }

        /// <summary>
        /// Chiến lược embedding tự nhúng câu hỏi rồi pipeline nhúng lại ở nhánh truy hồi. Bình
        /// thường lần thứ hai là trúng cache nên tổng vẫn là một lượt gọi API — nhưng tắt cache thì
        /// mỗi request RAG tốn HAI lượt, đúng vào nút thắt hạn mức 100 request/phút.
        /// Chỉ cảnh báo chứ không chặn: tổ hợp này hợp lệ, chỉ là tốn hơn.
        /// </summary>
        private static void WarnIfQueryCacheDisabled(IServiceCollection services, IConfiguration configuration)
        {
            var cacheConfig = configuration.GetSection(QueryCacheConfig.SectionName).Get<QueryCacheConfig>();

            if (cacheConfig is not null && !cacheConfig.Enabled)
            {
                services.AddSingleton<IHostedService>(sp =>
                    new StartupWarning(
                        sp.GetRequiredService<ILogger<StartupWarning>>(),
                        "SemanticRouter:Strategy = Embedding nhưng QueryCache:Enabled = false. Router sẽ nhúng " +
                        "câu hỏi một lần để định tuyến và nhánh truy hồi nhúng lại lần nữa, tức là MỖI request " +
                        "RAG tốn hai lượt gọi API embedding thay vì một. Bật QueryCache để lần thứ hai trúng cache."));
            }
        }

        /// <summary>
        /// Nổ ngay lúc khởi động khi cấu hình còn dùng khóa cũ đã dời chỗ.
        /// Lý do phải nổ thay vì chỉ log nằm ở <see cref="ObsoleteRouterKeys"/>.
        /// </summary>
        private static void ThrowIfObsoleteKeysPresent(IConfigurationSection section)
        {
            var present = ObsoleteRouterKeys.Moved
                .Where(entry => section[entry.Key] is not null)
                .ToList();

            if (present.Count == 0)
                return;

            var message = new StringBuilder(ObsoleteRouterKeys.MessageHeader).AppendLine().AppendLine();

            foreach (var entry in present)
            {
                message.AppendLine(string.Format(ObsoleteRouterKeys.MessageLineFormat,
                    $"{SemanticRouterConfig.SectionName}:{entry.Key}", entry.Value));
            }

            message.AppendLine().Append(ObsoleteRouterKeys.MessageFooter);

            throw new InvalidOperationException(message.ToString());
        }

        /// <summary>
        /// Ghi một dòng cảnh báo lúc host khởi động. Là hosted service chứ không phải một lệnh log
        /// thẳng trong hàm đăng ký vì lúc đăng ký chưa có <c>ILogger</c> nào để dùng.
        /// </summary>
        private sealed class StartupWarning : IHostedService
        {
            private readonly ILogger _logger;
            private readonly string _message;

            public StartupWarning(ILogger logger, string message)
            {
                _logger = logger;
                _message = message;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                _logger.LogWarning("{Message}", _message);
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
