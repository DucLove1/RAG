using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using RAG.Class.Caching;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký cache cho đường hỏi đáp và service ghi cache xuống đĩa.</summary>
    public static class QueryCacheServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký cache cho đường hỏi đáp. Khi tắt sẽ dùng Null Object để các decorator
        /// không cần rẽ nhánh theo cờ bật/tắt.
        /// </summary>
        public static IServiceCollection AddQueryCache(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(QueryCacheConfig.SectionName);
            services.Configure<QueryCacheConfig>(section);

            var config = section.Get<QueryCacheConfig>() ?? new QueryCacheConfig();

            if (!config.Enabled)
            {
                services.AddSingleton<NullQueryCache>();
                services.AddSingleton<INormalizationCache>(sp => sp.GetRequiredService<NullQueryCache>());
                services.AddSingleton<IEmbeddingCache>(sp => sp.GetRequiredService<NullQueryCache>());
                services.AddSingleton<IRouteDecisionCache>(sp => sp.GetRequiredService<NullQueryCache>());
                services.AddSingleton<IQueryCacheStatistics>(sp => sp.GetRequiredService<NullQueryCache>());
                return services;
            }

            services.AddSingleton(sp =>
            {
                // Lấy model và số chiều từ configuration chứ không từ IEmbeddingProvider: provider
                // đã bị bọc bởi decorator cache nên phụ thuộc ngược lại sẽ tạo vòng lặp trong container.
                var embedding = sp.GetRequiredService<IOptions<GeminiEmbeddingModelConfig>>().Value;

                return new MemoryQueryCache(
                    embedding.Model,
                    embedding.OutputDimensions,
                    sp.GetRequiredService<IOptions<QueryCacheConfig>>());
            });

            // Đăng ký kép qua factory là bắt buộc: đăng ký riêng lẻ nhiều lần thì container tạo
            // NHIỀU instance, và service flush sẽ lưu cái cache mà pipeline không hề dùng.
            services.AddSingleton<INormalizationCache>(sp => sp.GetRequiredService<MemoryQueryCache>());
            services.AddSingleton<IEmbeddingCache>(sp => sp.GetRequiredService<MemoryQueryCache>());
            services.AddSingleton<IRouteDecisionCache>(sp => sp.GetRequiredService<MemoryQueryCache>());
            services.AddSingleton<IQueryCacheStatistics>(sp => sp.GetRequiredService<MemoryQueryCache>());
            services.AddSingleton<IPersistableQueryCache>(sp => sp.GetRequiredService<MemoryQueryCache>());

            if (string.IsNullOrWhiteSpace(config.PersistPath))
            {
                services.AddSingleton<IQueryCacheStore, NullQueryCacheStore>();
                return services;
            }

            services.AddSingleton<IQueryCacheStore, FileQueryCacheStore>();
            services.AddHostedService<QueryCachePersistenceService>();

            return services;
        }
    }
}
