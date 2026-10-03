using Microsoft.Extensions.Options;
using RAG.Class;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký client Jev (TypeSafe System One) cùng rotator key và named HttpClient.</summary>
    public static class TypeSafeServiceCollectionExtensions
    {
        /// <summary>
        /// CHỈ được gọi từ chiến lược định tuyến Jev. Đăng ký ở composition root chung thì
        /// <c>ValidateOnStart</c> sẽ làm app từ chối khởi động vì thiếu key TypeSafe ngay cả khi đang
        /// chạy chiến lược khác — mà chiến lược khác thì không cần key đó.
        /// </summary>
        public static IServiceCollection AddTypeSafeJev(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatedOptions<TypeSafeConfig>(configuration, TypeSafeConfig.SectionName);

            services.AddKeyedSingleton<IApiKeyRotator>(ApiKeyPoolKey.TypeSafe, (sp, _) =>
            {
                var options = sp.GetRequiredService<IOptions<TypeSafeConfig>>().Value;
                return new ApiKeyRotator(
                    options.ApiKeys,
                    "TypeSafe Jev",
                    TimeSpan.FromSeconds(options.RateLimitCooldownSeconds),
                    sp.GetRequiredService<ILogger<ApiKeyRotator>>());
            });

            // Cấu hình HttpClient thuộc về composition root; key gắn theo từng request trong client.
            services.AddHttpClient(HttpClientNames.TypeSafe, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<TypeSafeConfig>>().Value;

                client.BaseAddress = OptionsRegistration.BuildBaseAddress(options.Url);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

            services.AddSingleton<IJevClient>(sp =>
            {
                var rotator = sp.GetRequiredKeyedService<IApiKeyRotator>(ApiKeyPoolKey.TypeSafe);
                return ActivatorUtilities.CreateInstance<TypeSafeJevClient>(sp, rotator);
            });

            return services;
        }
    }
}
