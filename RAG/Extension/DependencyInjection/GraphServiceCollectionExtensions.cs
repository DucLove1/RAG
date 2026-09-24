using Microsoft.Extensions.Options;
using Neo4j.Driver;
using RAG.Class.Config;
using RAG.Class.Graph;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký tầng đồ thị tri thức (GraphRAG).</summary>
    public static class GraphServiceCollectionExtensions
    {
        public static IServiceCollection AddGraph(this IServiceCollection services,
                                                  IConfiguration configuration,
                                                  IHostEnvironment environment)
        {
            services.AddValidatedOptions<GraphConfig>(configuration, GraphConfig.SectionName);

            // Ngoài nhánh Enabled: AskContextBuilder đọc ngân sách ngữ cảnh ở cả hai trạng thái.
            // Không có trường [Required] nào nên không chặn khởi động của máy không có Neo4j.
            services.AddValidatedOptions<GraphSearchConfig>(configuration, GraphSearchConfig.SectionName);

            var config = configuration.GetSection(GraphConfig.SectionName).Get<GraphConfig>() ?? new GraphConfig();

            if (!config.Enabled)
                return services.AddDisabledGraph();

            // Options của Neo4j CHỈ đăng ký trong nhánh này. Đưa lên trên lời gọi Enabled thì
            // [Required] Password cộng ValidateOnStart sẽ chặn khởi động của mọi máy không có
            // Neo4j — cùng cái bẫy mà AddSemanticAnswerCache đã ghi lại.
            services.AddValidatedOptions<Neo4jConfig>(configuration, Neo4jConfig.SectionName);
            services.AddValidatedOptions<GraphLoaderConfig>(configuration, GraphLoaderConfig.SectionName);
            services.AddValidatedOptions<GraphSchemaConfig>(configuration, GraphSchemaConfig.SectionName);
            services.AddValidatedOptions<GraphExtractionConfig>(configuration, GraphExtractionConfig.SectionName);

            // IDriver là SINGLETON vì nó CHÍNH LÀ connection pool và thread-safe. Dựng theo request
            // nghĩa là bắt tay TLS lại với Aura ở mỗi câu hỏi (hàng trăm ms) và rò socket.
            // GraphDatabase.Driver không kết nối ngay, nên một Neo4j chết không tốn gì lúc khởi động.
            services.AddSingleton<IDriver>(provider =>
            {
                var neo4j = provider.GetRequiredService<IOptions<Neo4jConfig>>().Value;

                return GraphDatabase.Driver(
                    neo4j.Uri,
                    AuthTokens.Basic(neo4j.Username, neo4j.Password),
                    builder => builder
                        .WithConnectionTimeout(TimeSpan.FromMilliseconds(neo4j.ConnectTimeoutMs))
                        .WithMaxTransactionRetryTime(TimeSpan.FromMilliseconds(neo4j.MaxTransactionRetryTimeMs))
                        .WithMaxConnectionPoolSize(neo4j.MaxConnectionPoolSize));
            });

            services.AddSingleton<IOntology, JsonOntology>();

            // Đăng ký lớp cụ thể MỘT lần rồi trỏ hai interface về đúng instance đó: đăng ký rời sẽ
            // dựng hai instance với hai bộ đếm và hai bộ ngắt mạch riêng, và endpoint số liệu sẽ
            // báo toàn số 0 trong khi mạch thật đang hở.
            services.AddSingleton<Neo4jGraphStore>();
            services.AddSingleton<IGraphStore>(provider => provider.GetRequiredService<Neo4jGraphStore>());
            services.AddSingleton<IGraphStatistics>(provider => provider.GetRequiredService<Neo4jGraphStore>());
            services.AddSingleton<IGraphWarmup>(provider => provider.GetRequiredService<Neo4jGraphStore>());

            if (configuration.GetSection(Neo4jConfig.SectionName).Get<Neo4jConfig>()?.WarmupEnabled ?? true)
                services.AddHostedService<GraphWarmupService>();

            // Hạt giống của đồ thị: LLM chọn thực thể trong danh mục NPC được biết. Không cache kết quả
            // trích — cache câu trả lời theo ngữ nghĩa đã chặn trước bước này ở mọi câu hỏi lặp lại.
            services.AddSingleton<IGraphEntityCatalog, CachedGraphEntityCatalog>();
            services.AddSingleton<IGraphEntityExtractor, LlmGraphEntityExtractor>();

            services.AddSingleton<IGraphSearch, EntityGraphSearch>();
            services.AddSingleton<IGraphContextRenderer, OntologyGraphContextRenderer>();

            return services.AddGraphAdmin(configuration, environment);
        }

        /// <summary>
        /// Null Object thay vì rải if trong đường trả lời: tầng dựng ngữ cảnh không cần biết đồ thị
        /// có tồn tại hay không.
        /// </summary>
        private static IServiceCollection AddDisabledGraph(this IServiceCollection services)
        {
            services.AddSingleton<IGraphSearch, NullGraphSearch>();
            services.AddSingleton<IGraphContextRenderer, NullGraphContextRenderer>();

            // Vẫn phải đăng ký thống kê và đường quản trị: GraphAdminController luôn được MVC dựng,
            // và thiếu một phụ thuộc thì nó chết bằng 500 thay vì 404. Null Object cho cả hai để
            // controller không cần biết đồ thị có tồn tại hay không.
            services.AddSingleton<IGraphStatistics, NullGraphSearch>();

            // Controller cần danh mục cho catalog-debug, còn tầng đo độ trễ bọc bộ trích vô điều kiện:
            // thiếu một trong hai là 500 ở controller hoặc chết lúc khởi động ở Decorate.
            services.AddSingleton<IGraphEntityCatalog, NullGraphEntityCatalog>();
            services.AddSingleton<IGraphEntityExtractor, NullGraphEntityExtractor>();

            return services.AddDisabledGraphAdmin();
        }

        /// <summary>
        /// Đường quản trị chỉ mở khi CẢ HAI điều kiện cùng đúng: được bật trong cấu hình, và đang
        /// chạy ở Development. Nạp đồ thị là thao tác ghi đè dữ liệu — một cờ cấu hình lỡ tay bật
        /// trên môi trường thật không nên đủ để mở nó.
        /// </summary>
        private static IServiceCollection AddGraphAdmin(this IServiceCollection services,
                                                        IConfiguration configuration,
                                                        IHostEnvironment environment)
        {
            var loader = configuration.GetSection(GraphLoaderConfig.SectionName).Get<GraphLoaderConfig>()
                         ?? new GraphLoaderConfig();

            if (!loader.Enabled || !environment.IsDevelopment())
                return services.AddDisabledGraphAdmin();

            services.AddSingleton<IGraphDataSource, JsonGraphDataSource>();

            services.AddSingleton<Neo4jGraphLoader>();
            services.AddSingleton<IGraphLoader>(provider => provider.GetRequiredService<Neo4jGraphLoader>());
            services.AddSingleton<IGraphSchemaAdmin>(provider => provider.GetRequiredService<Neo4jGraphLoader>());

            return services;
        }

        private static IServiceCollection AddDisabledGraphAdmin(this IServiceCollection services)
        {
            services.AddSingleton<DisabledGraphAdmin>();
            services.AddSingleton<IGraphLoader>(provider => provider.GetRequiredService<DisabledGraphAdmin>());
            services.AddSingleton<IGraphSchemaAdmin>(provider => provider.GetRequiredService<DisabledGraphAdmin>());

            return services;
        }
    }
}
