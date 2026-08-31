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
    /// <summary>Đăng ký các nhà cung cấp LLM, rotator key, và bộ chọn provider mặc định.</summary>
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
            // Rotator key cho pool Groq.
            services.AddKeyedSingleton<IApiKeyRotator>(ApiKeyPoolKey.Groq, (sp, _) =>
            {
                var options = sp.GetRequiredService<IOptions<GroqConfig>>().Value;
                return new ApiKeyRotator(
                    options.ApiKeys,
                    "Groq",
                    TimeSpan.FromSeconds(options.RateLimitCooldownSeconds),
                    sp.GetRequiredService<ILogger<ApiKeyRotator>>());
            });

            services.AddKeyedSingleton<ILLMProvider, GroqCloudProvider>(LlmProviderKey.Groq);

            return services;
        }

        private static IServiceCollection AddGeminiLlmProvider(this IServiceCollection services)
        {
            // Rotator key cho pool Gemini LLM.
            services.AddKeyedSingleton<IApiKeyRotator>(ApiKeyPoolKey.GeminiLlm, (sp, _) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiLlmConfig>>().Value;
                return new ApiKeyRotator(
                    options.ApiKeys,
                    "Gemini LLM",
                    TimeSpan.FromSeconds(options.RateLimitCooldownSeconds),
                    sp.GetRequiredService<ILogger<ApiKeyRotator>>());
            });

            // HttpClient không còn bake key vào header — provider sẽ attach per request.
            services.AddHttpClient(HttpClientNames.GeminiLlm, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiLlmConfig>>().Value;

                client.BaseAddress = OptionsRegistration.BuildBaseAddress(options.Url);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

            services.AddKeyedSingleton<ILLMProvider, GeminiLLMProvider>(LlmProviderKey.Gemini);

            return services;
        }
    }
}
