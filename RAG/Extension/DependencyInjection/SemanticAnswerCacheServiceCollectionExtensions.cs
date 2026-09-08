using Microsoft.Extensions.Options;
using RAG.Class.Caching.Redis;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký tầng cache câu trả lời theo ngữ nghĩa.</summary>
    public static class SemanticAnswerCacheServiceCollectionExtensions
    {
        public static IServiceCollection AddSemanticAnswerCache(this IServiceCollection services,
                                                                IConfiguration configuration)
        {
            services.AddValidatedOptions<SemanticAnswerCacheConfig>(configuration, SemanticAnswerCacheConfig.SectionName);

            var config = configuration.GetSection(SemanticAnswerCacheConfig.SectionName).Get<SemanticAnswerCacheConfig>()
                         ?? new SemanticAnswerCacheConfig();

            if (!config.Enabled)
            {
                // Null Object thay vì rải if trong AskPipeline: đường trả lời không cần biết tầng
                // cache này có tồn tại hay không.
                services.AddSingleton<NullSemanticAnswerCache>();
                services.AddSingleton<ISemanticAnswerCache>(sp => sp.GetRequiredService<NullSemanticAnswerCache>());
                services.AddSingleton<ISemanticAnswerCacheStatistics>(sp => sp.GetRequiredService<NullSemanticAnswerCache>());
                services.AddSingleton<ISemanticAnswerCacheAdmin>(sp => sp.GetRequiredService<NullSemanticAnswerCache>());

                return services;
            }

            services.AddSingleton<IRedisConnection, RedisConnectionProvider>();

            // Đăng ký lớp cụ thể MỘT lần rồi trỏ từng interface về đúng instance đó. Đăng ký rời
            // từng interface (AddSingleton<ISemanticAnswerCache, RedisSemanticAnswerCache>() và
            // tương tự) sẽ dựng BA instance độc lập: ba bộ đếm số liệu rời nhau, ba cờ _indexEnsured
            // rời nhau, và endpoint cache-stats sẽ báo toàn số 0 trong khi cache vẫn đang chạy.
            services.AddSingleton<RedisSemanticAnswerCache>();
            services.AddSingleton<ISemanticAnswerCache>(sp => sp.GetRequiredService<RedisSemanticAnswerCache>());
            services.AddSingleton<ISemanticAnswerCacheStatistics>(sp => sp.GetRequiredService<RedisSemanticAnswerCache>());
            services.AddSingleton<ISemanticAnswerCacheAdmin>(sp => sp.GetRequiredService<RedisSemanticAnswerCache>());

            return services;
        }
    }
}
