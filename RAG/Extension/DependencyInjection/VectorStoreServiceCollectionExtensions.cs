using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using Qdrant.Client;
using RAG.Class.Retrieval;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký kho vector.</summary>
    public static class VectorStoreServiceCollectionExtensions
    {
        public static IServiceCollection AddQdrant(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatedOptions<QDrantConfig>(configuration, QDrantConfig.SectionName);
            services.AddSingleton<IVectorStore, QdrantVectorStore>();
            services.AddSingleton<QdrantClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<QDrantConfig>>().Value;
                return new QdrantClient(options.Host, options.Port, https: options.UseHttps, apiKey: options.ApiKey);
            });

            return services;
        }
    }
}
