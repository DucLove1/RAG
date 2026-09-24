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
            // Đăng ký lớp cụ thể MỘT lần rồi trỏ từng interface về đúng instance đó. Đăng ký rời
            // (AddSingleton<IVectorStore, QdrantVectorStore>() và tương tự cho IChunkTextLookup) sẽ
            // dựng HAI instance độc lập, mỗi cái một cờ _ensured riêng — cùng cái bẫy mà
            // AddSemanticAnswerCache đã ghi lại, và ở đây nó còn nghĩa là hai QdrantClient.
            services.AddSingleton<QdrantVectorStore>();
            services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<QdrantVectorStore>());
            services.AddSingleton<IChunkTextLookup>(sp => sp.GetRequiredService<QdrantVectorStore>());
            services.AddSingleton<QdrantClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<QDrantConfig>>().Value;
                return new QdrantClient(options.Host, options.Port, https: options.UseHttps, apiKey: options.ApiKey);
            });

            return services;
        }
    }
}
