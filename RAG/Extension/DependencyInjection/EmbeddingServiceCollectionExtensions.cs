using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using RAG.Class;
using RAG.Class.Caching;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký nhà cung cấp embedding cùng rotator key và decorator cache bọc quanh nó.</summary>
    public static class EmbeddingServiceCollectionExtensions
    {
        public static IServiceCollection AddEmbeddingModel(this IServiceCollection services, IConfiguration configuration)
        {
            // Section vừa đổi tên; lý do phải nổ thay vì chỉ log nằm ở ObsoleteEmbeddingKeys.
            if (configuration.GetSection(ObsoleteEmbeddingKeys.ObsoleteSectionName).Exists())
                throw new InvalidOperationException(ObsoleteEmbeddingKeys.Message);

            services.AddValidatedOptions<GeminiEmbeddingModelConfig>(
                configuration, GeminiEmbeddingModelConfig.SectionName);

            // Rotator key cho pool Gemini Embedding.
            services.AddKeyedSingleton<IApiKeyRotator>(ApiKeyPoolKey.GeminiEmbedding, (sp, _) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiEmbeddingModelConfig>>().Value;
                return new ApiKeyRotator(
                    options.ApiKeys,
                    "Gemini Embedding",
                    TimeSpan.FromSeconds(options.RateLimitCooldownSeconds),
                    sp.GetRequiredService<ILogger<ApiKeyRotator>>());
            });

            // Cấu hình HttpClient thuộc về composition root, không phải constructor của provider.
            // BaseAddress là base url, còn đường dẫn của từng endpoint do provider dựng từ Model —
            // nhờ vậy tên model chỉ khai MỘT lần, thay vì lặp lại trong hai URL tuyệt đối như trước.
            // Không còn bake key vào header — provider sẽ attach per request.
            services.AddHttpClient(HttpClientNames.GeminiEmbedding, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiEmbeddingModelConfig>>().Value;

                client.BaseAddress = OptionsRegistration.BuildBaseAddress(options.Url);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

            // Bọc cache quanh provider thật (Decorator): mọi consumer vẫn chỉ thấy IEmbeddingProvider.
            services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var rotator = sp.GetRequiredKeyedService<IApiKeyRotator>(ApiKeyPoolKey.GeminiEmbedding);
                var inner = ActivatorUtilities.CreateInstance<GeminiEmbeddingProvider>(sp, rotator);

                return new CachingEmbeddingProvider(
                    inner,
                    sp.GetRequiredService<IEmbeddingCache>(),
                    sp.GetRequiredService<ILogger<CachingEmbeddingProvider>>());
            });

            return services;
        }
    }
}
