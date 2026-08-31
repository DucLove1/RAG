using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using RAG.Class;
using RAG.Class.Caching;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký nhà cung cấp embedding cùng decorator cache bọc quanh nó.</summary>
    public static class EmbeddingServiceCollectionExtensions
    {
        public static IServiceCollection AddEmbeddingModel(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatedOptions<GeminiEmbeddingModelConfig>(
                configuration, GeminiEmbeddingModelConfig.SectionName);

            // Cấu hình HttpClient thuộc về composition root, không phải constructor của provider.
            // Không đặt BaseAddress: Url và BatchUrl đều là URL tuyệt đối, dùng luôn cho rõ ràng.
            services.AddHttpClient(HttpClientNames.GeminiEmbedding, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiEmbeddingModelConfig>>().Value;

                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Remove(GeminiApiDefaults.ApiKeyHeader);
                client.DefaultRequestHeaders.Add(GeminiApiDefaults.ApiKeyHeader, options.ApiKey);
            });

            // Bọc cache quanh provider thật (Decorator): mọi consumer vẫn chỉ thấy IEmbeddingProvider.
            services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var inner = ActivatorUtilities.CreateInstance<GeminiEmbeddingProvider>(sp);

                return new CachingEmbeddingProvider(
                    inner,
                    sp.GetRequiredService<IEmbeddingCache>(),
                    sp.GetRequiredService<ILogger<CachingEmbeddingProvider>>());
            });

            return services;
        }
    }
}
