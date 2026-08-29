using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Qdrant.Client;
using RAG.Class;
using RAG.Class.Caching;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Normalization;
using RAG.Class.Routing;
using RAG.Interface;
using System.ClientModel;

namespace RAG.Extension
{
    public static class Extension
    {
        /// <summary>
        /// Đăng ký toàn bộ các <see cref="ILLMProvider"/> dưới dạng Keyed Services
        /// (cùng interface, khác khóa) và một đăng ký không khóa trỏ tới provider mặc định.
        /// </summary>
        public static IServiceCollection AddLLM(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<GroqConfig>(configuration.GetSection(GroqConfig.SectionName));
            services.Configure<GeminiLlmConfig>(configuration.GetSection(GeminiLlmConfig.SectionName));
            services.Configure<LlmSelectionConfig>(configuration.GetSection(LlmSelectionConfig.SectionName));

            services.AddGroqProvider();
            services.AddGeminiLlmProvider();

            services.AddSingleton<ILlmProviderResolver, KeyedLlmProviderResolver>();

            // Provider mặc định (dùng để sinh câu trả lời) được chọn qua cấu hình LLM:Provider.
            services.AddSingleton<ILLMProvider>(sp =>
            {
                var selection = sp.GetRequiredService<IOptions<LlmSelectionConfig>>().Value;
                return sp.GetRequiredService<ILlmProviderResolver>().Resolve(selection.Provider);
            });

            return services;
        }

        private static IServiceCollection AddGroqProvider(this IServiceCollection services)
        {
            services.AddSingleton<ChatClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<GroqConfig>>().Value;

                return new ChatClient(
                    model: options.Model,
                    credential: new ApiKeyCredential(options.ApiKey),
                    options: new OpenAIClientOptions { Endpoint = new Uri(options.Url) });
            });

            services.AddKeyedSingleton<ILLMProvider, GroqCloudProvider>(LlmProviderKey.Groq);

            return services;
        }

        private static IServiceCollection AddGeminiLlmProvider(this IServiceCollection services)
        {
            services.AddHttpClient(HttpClientNames.GeminiLlm, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiLlmConfig>>().Value;

                client.BaseAddress = BuildBaseAddress(options.Url);
                client.DefaultRequestHeaders.Remove(GeminiApiDefaults.ApiKeyHeader);
                client.DefaultRequestHeaders.Add(GeminiApiDefaults.ApiKeyHeader, options.ApiKey);
            });

            services.AddKeyedSingleton<ILLMProvider, GeminiLLMProvider>(LlmProviderKey.Gemini);

            return services;
        }

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
                    sp.GetRequiredService<IQueryCache>(),
                    sp.GetRequiredService<ILogger<CachingQueryNormalizer>>());
            });

            return services;
        }

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
                services.AddSingleton<IQueryCache, NullQueryCache>();
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

            // Đăng ký kép qua factory là bắt buộc: đăng ký riêng lẻ hai lần thì container tạo HAI
            // instance, và service flush sẽ lưu cái cache mà pipeline không hề dùng.
            services.AddSingleton<IQueryCache>(sp => sp.GetRequiredService<MemoryQueryCache>());

            if (string.IsNullOrWhiteSpace(config.PersistPath))
            {
                services.AddSingleton<IQueryCacheStore, NullQueryCacheStore>();
                return services;
            }

            services.AddSingleton<IQueryCacheStore, FileQueryCacheStore>();
            services.AddHostedService<QueryCachePersistenceService>();

            return services;
        }

        /// <summary>
        /// Đăng ký node định tuyến ngữ nghĩa. Khi bị tắt — hoặc không khai báo route hợp lệ nào —
        /// sẽ dùng Null Object thay vì rải if trong pipeline.
        /// </summary>
        public static IServiceCollection AddSemanticRouter(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(SemanticRouterConfig.SectionName);
            services.Configure<SemanticRouterConfig>(section);

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
            services.AddSingleton<EmbeddingSemanticRouter>();
            services.AddSingleton<ISemanticRouter>(sp => sp.GetRequiredService<EmbeddingSemanticRouter>());
            services.AddHostedService<SemanticRouterWarmupService>();

            return services;
        }

        public static IServiceCollection AddEmbeddingModel(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<GeminiEmbeddingModelConfig>(
                configuration.GetSection(GeminiEmbeddingModelConfig.SectionName));
            services.AddHttpClient<GeminiEmbeddingProvider>();

            // Bọc cache quanh provider thật (Decorator): mọi consumer vẫn chỉ thấy IEmbeddingProvider.
            services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var inner = ActivatorUtilities.CreateInstance<GeminiEmbeddingProvider>(sp);

                return new CachingEmbeddingProvider(
                    inner,
                    sp.GetRequiredService<IQueryCache>(),
                    sp.GetRequiredService<ILogger<CachingEmbeddingProvider>>());
            });

            return services;
        }

        public static IServiceCollection AddQdrant(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<QDrantConfig>(
                configuration.GetSection(QDrantConfig.SectionName));
            services.AddSingleton<IQdrantProvider, QdrantProvider>();
            services.AddSingleton<QDrantConfig>(sp => sp.GetRequiredService<IOptions<QDrantConfig>>().Value);
            services.AddSingleton<QdrantClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<QDrantConfig>>().Value;
                return new QdrantClient(options.Host, options.Port, https: true, apiKey: options.ApiKey);
            });

            return services;
        }

        /// <summary>
        /// Đăng ký pipeline RAG cùng các cấu hình prompt/chunking của nó.
        /// </summary>
        public static IServiceCollection AddRagPipeline(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RagConfig>(configuration.GetSection(RagConfig.SectionName));
            services.Configure<PromptConfig>(configuration.GetSection(PromptConfig.SectionName));

            services.AddSingleton<RagConfig>(sp => sp.GetRequiredService<IOptions<RagConfig>>().Value);
            services.AddSingleton<RAGPipline>();

            return services;
        }

        private static Uri BuildBaseAddress(string url) =>
            new(url.EndsWith('/') ? url : url + '/');
    }
}
