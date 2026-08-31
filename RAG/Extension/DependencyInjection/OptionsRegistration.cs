using Microsoft.Extensions.Options;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Tiện ích dùng chung cho các module đăng ký.</summary>
    public static class OptionsRegistration
    {
        internal static IServiceCollection AddValidatedOptions<TOptions>(this IServiceCollection services,
                                                                        IConfiguration configuration,
                                                                        string sectionName)
            where TOptions : class
        {
            services.AddOptions<TOptions>()
                    .Bind(configuration.GetSection(sectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            return services;
        }

        internal static Uri BuildBaseAddress(string url) =>
            new(url.EndsWith('/') ? url : url + '/');
    }
}
