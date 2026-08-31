using RAG.Class.Config;
using RAG.Extension.Errors;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký ProblemDetails và bộ ánh xạ exception sang mã HTTP.</summary>
    public static class ErrorHandlingServiceCollectionExtensions
    {
        /// <summary>
        /// Bật ProblemDetails và bộ ánh xạ exception sang mã HTTP.
        /// </summary>
        public static IServiceCollection AddErrorHandling(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ErrorResponseConfig>(configuration.GetSection(ErrorResponseConfig.SectionName));

            services.AddProblemDetails();
            services.AddExceptionHandler<RagExceptionHandler>();

            return services;
        }
    }
}
