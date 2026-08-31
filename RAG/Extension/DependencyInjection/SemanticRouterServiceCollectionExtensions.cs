using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using RAG.Class.Routing;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký node định tuyến ngữ nghĩa và warm-up của nó.</summary>
    public static class SemanticRouterServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký node định tuyến ngữ nghĩa. Khi bị tắt — hoặc không khai báo route hợp lệ nào —
        /// sẽ dùng Null Object thay vì rải if trong pipeline.
        /// </summary>
        public static IServiceCollection AddSemanticRouter(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(SemanticRouterConfig.SectionName);
            services.Configure<SemanticRouterConfig>(section);
            services.Configure<RouteMessagesConfig>(configuration.GetSection(RouteMessagesConfig.SectionName));

            var config = section.Get<SemanticRouterConfig>() ?? new SemanticRouterConfig();

            // Bật nhưng không có route nào dùng được thì coi như tắt: tránh warm-up vô nghĩa
            // và tránh chạy vòng so khớp rỗng ở mọi request.
            var hasUsableRoute = config.Routes.Any(route =>
                !string.IsNullOrWhiteSpace(route.Name) &&
                !string.IsNullOrWhiteSpace(route.UserPromptTemplate) &&
                route.Utterances.Any(utterance => !string.IsNullOrWhiteSpace(utterance)));

            if (!config.Enabled || !hasUsableRoute)
            {
                services.AddSingleton<ISemanticRouter, PassthroughSemanticRouter>();
                return services;
            }

            if (string.IsNullOrWhiteSpace(config.VectorCachePath))
                services.AddSingleton<IRouteVectorCache, NullRouteVectorCache>();
            else
                services.AddSingleton<IRouteVectorCache, FileRouteVectorCache>();

            if (string.IsNullOrWhiteSpace(config.UtteranceStorePath))
                services.AddSingleton<IRouteUtteranceStore, NullRouteUtteranceStore>();
            else
                services.AddSingleton<IRouteUtteranceStore, FileRouteUtteranceStore>();

            // Đăng ký kép qua factory là bắt buộc: nếu đăng ký riêng lẻ hai lần thì container sẽ tạo
            // HAI instance với hai cache khác nhau, và warm-up sẽ làm ấm cái router mà pipeline không dùng.
            // Trạng thái dùng chung của node định tuyến. Cả ba lớp cộng tác phải thấy CÙNG một
            // instance, nếu không warm-up sẽ làm ấm một bảng route mà router không hề dùng.
            services.AddSingleton<RouteCatalog>();

            services.AddSingleton<IRouteScorer, MaxSimilarityScorer>();
            services.AddSingleton<RouteUtteranceAdmin>();

            services.AddSingleton<ISemanticRouter, EmbeddingSemanticRouter>();
            services.AddSingleton<IRouterWarmup, RouteCatalogBuilder>();
            services.AddHostedService<SemanticRouterWarmupService>();

            return services;
        }
    }
}
