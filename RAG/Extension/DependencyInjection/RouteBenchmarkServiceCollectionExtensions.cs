using RAG.Class.Config;
using RAG.Class.Routing;
using RAG.Class.Routing.Benchmark;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký benchmark so sánh các chiến lược định tuyến.</summary>
    public static class RouteBenchmarkServiceCollectionExtensions
    {
        /// <summary>
        /// PHẢI gọi SAU <c>AddSemanticRouter</c>: chiến lược đang chạy phải được đăng ký trước để
        /// TryAdd của <see cref="IRouteUtteranceAdmin"/> giữ đúng hành vi của nó.
        /// <para>
        /// Chỉ mở khi được bật VÀ đang ở Development, cùng chính sách với đường quản trị đồ thị:
        /// bật nghĩa là đăng ký thêm mọi chiến lược (kể cả đòi key TypeSafe) và mỗi lần chạy đốt
        /// vài chục lượt gọi model.
        /// </para>
        /// </summary>
        public static IServiceCollection AddRouteBenchmark(this IServiceCollection services,
                                                           IConfiguration configuration,
                                                           IHostEnvironment environment)
        {
            services.AddValidatedOptions<RouteBenchmarkConfig>(configuration, RouteBenchmarkConfig.SectionName);

            var config = configuration.GetSection(RouteBenchmarkConfig.SectionName).Get<RouteBenchmarkConfig>()
                         ?? new RouteBenchmarkConfig();

            if (!config.Enabled || !environment.IsDevelopment())
            {
                services.AddSingleton<IRouteBenchmark, DisabledRouteBenchmark>();
                return services;
            }

            foreach (var strategy in config.Strategies.Distinct())
                services.AddRouterStrategy(strategy, configuration);

            services.AddSingleton<IRouteExplainerResolver, KeyedRouteExplainerResolver>();
            services.AddSingleton<IRouteBenchmarkCaseSource, CsvRouteBenchmarkCaseSource>();
            services.AddSingleton<IRouteBenchmark, RouteBenchmarkRunner>();

            return services;
        }
    }
}
