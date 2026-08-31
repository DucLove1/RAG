using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using OpenAI;
using OpenAI.Chat;
using RAG.Class;
using System.ClientModel;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký các nhà cung cấp LLM và bộ chọn provider mặc định.</summary>
    public static class LlmServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký toàn bộ các <see cref="ILLMProvider"/> dưới dạng Keyed Services
        /// (cùng interface, khác khóa) và một đăng ký không khóa trỏ tới provider mặc định.
        /// </summary>
        public static IServiceCollection AddLLM(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatedOptions<GroqConfig>(configuration, GroqConfig.SectionName);
            services.AddValidatedOptions<GeminiLlmConfig>(configuration, GeminiLlmConfig.SectionName);
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

                client.BaseAddress = OptionsRegistration.BuildBaseAddress(options.Url);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Remove(GeminiApiDefaults.ApiKeyHeader);
                client.DefaultRequestHeaders.Add(GeminiApiDefaults.ApiKeyHeader, options.ApiKey);
            });

            services.AddKeyedSingleton<ILLMProvider, GeminiLLMProvider>(LlmProviderKey.Gemini);

            return services;
        }
    }
}
