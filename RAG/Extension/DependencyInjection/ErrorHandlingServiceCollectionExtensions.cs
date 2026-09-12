using RAG.Class.Config;
using RAG.Extension.Errors;
using RAG.Interface;

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

            // Đăng ký bộ ánh xạ RIÊNG chứ không để nó là chi tiết bên trong bộ xử lý exception:
            // tầng SSE cũng cần đúng phép ánh xạ đó cho những lỗi xảy ra sau khi header đã bay đi.
            services.AddSingleton<IRagErrorMapper, RagErrorMapper>();

            services.AddProblemDetails();
            services.AddExceptionHandler<RagExceptionHandler>();

            return services;
        }
    }
}
