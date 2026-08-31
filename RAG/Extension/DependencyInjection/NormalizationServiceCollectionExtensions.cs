using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using RAG.Class.Caching;
using RAG.Class.Normalization;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký node chuẩn hóa câu hỏi.</summary>
    public static class NormalizationServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký node chuẩn hóa câu hỏi. Khi bị tắt sẽ dùng Null Object thay vì rải if trong pipeline.
        /// </summary>
        public static IServiceCollection AddQueryNormalization(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(QueryNormalizationConfig.SectionName);
            services.Configure<QueryNormalizationConfig>(section);

            var config = section.Get<QueryNormalizationConfig>() ?? new QueryNormalizationConfig();

            // Đăng ký bản thật dưới dạng chính nó, rồi bọc cache ở đăng ký interface.
            // Nhờ vậy pipeline không biết có cache hay không (Decorator).
            if (config.Enabled)
                services.AddSingleton<LlmQueryNormalizer>();

            services.AddSingleton<IQueryNormalizer>(sp =>
            {
                IQueryNormalizer inner = config.Enabled
                    ? sp.GetRequiredService<LlmQueryNormalizer>()
                    : new PassthroughQueryNormalizer();

                // Bọc cache là vô nghĩa với bản passthrough vì nó vốn đã không gọi ra ngoài.
                if (!config.Enabled)
                    return inner;

                return new CachingQueryNormalizer(
                    inner,
                    sp.GetRequiredService<INormalizationCache>(),
                    sp.GetRequiredService<ILogger<CachingQueryNormalizer>>());
            });

            return services;
        }
    }
}
