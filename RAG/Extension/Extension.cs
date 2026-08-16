using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Qdrant.Client;
using RAG.Class;
using RAG.Class.Config;
using RAG.Interface;
using System.ClientModel;

namespace RAG.Extension
{
    public static class Extension
    {
        public static IServiceCollection AddLLM(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<GroqConfig>(
            configuration.GetSection(GroqConfig.SectionName));

            services.AddSingleton<ChatClient>(sp =>
            {
                var options = sp
                    .GetRequiredService<IOptions<GroqConfig>>()
                    .Value;

                return new ChatClient(
                    model: options.Model,
                    credential: new ApiKeyCredential(options.ApiKey),
                    options: new OpenAIClientOptions
                    {
                        Endpoint = new Uri(
                            "https://api.groq.com/openai/v1"
                        )
                    });
            });

            services.AddSingleton<RAGPipline>();
            services.AddSingleton<ILLMProvider, GroqCloudProvider>();

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
                var options = sp
                    .GetRequiredService<IOptions<QDrantConfig>>()
                    .Value;
                return new QdrantClient(options.Host, options.Port, https: true, apiKey: options.ApiKey);
            });

            return services;
        }
    }
}
