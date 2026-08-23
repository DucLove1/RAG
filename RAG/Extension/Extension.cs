using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Qdrant.Client;
using RAG.Class;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Normalization;
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

            if (config.Enabled)
                services.AddSingleton<IQueryNormalizer, LlmQueryNormalizer>();
            else
                services.AddSingleton<IQueryNormalizer, PassthroughQueryNormalizer>();

            return services;
        }

        public static IServiceCollection AddEmbeddingModel(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<GeminiEmbeddingModelConfig>(
                configuration.GetSection(GeminiEmbeddingModelConfig.SectionName));
            services.AddHttpClient<GeminiEmbeddingProvider>();
            services.AddSingleton<IEmbeddingProvider, GeminiEmbeddingProvider>();
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
