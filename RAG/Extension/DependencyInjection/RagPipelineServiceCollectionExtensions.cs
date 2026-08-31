using RAG.Class;
using RAG.Class.Answering;
using RAG.Class.Config;
using RAG.Class.Routing;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký các service của đường trả lời và façade pipeline.</summary>
    public static class RagPipelineServiceCollectionExtensions
    {
        /// <summary>
        /// Mỗi vai trò trỏ tới ĐÚNG service làm việc đó, còn <see cref="IRagPipeline"/> trỏ tới
        /// façade. Nhờ vậy façade có thể nhận các vai trò qua constructor mà không tạo vòng phụ
        /// thuộc với chính nó.
        /// </summary>
        public static IServiceCollection AddRagPipeline(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatedOptions<RagConfig>(configuration, RagConfig.SectionName);
            services.AddValidatedOptions<PromptConfig>(configuration, PromptConfig.SectionName);

            services.AddSingleton<IAskService, AskPipeline>();
            services.AddSingleton<IRouteDiagnostics, RouteDiagnosticsService>();

            // IIngestionService đã được đăng ký ở AddIngestion; IRouteAdmin do façade đảm nhiệm
            // vì nó chỉ chuyển tiếp thẳng sang IRouteUtteranceAdmin.
            services.AddSingleton<RagPipeline>();
            services.AddSingleton<IRagPipeline>(sp => sp.GetRequiredService<RagPipeline>());
            services.AddSingleton<IRouteAdmin>(sp => sp.GetRequiredService<RagPipeline>());

            return services;
        }
    }
}
