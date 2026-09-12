using RAG.Class.Config;
using RAG.Class.Diagnostics;
using RAG.Class.Diagnostics.Timing;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>
    /// Đăng ký toàn bộ tầng đo độ trễ.
    /// <para>
    /// PHẢI gọi CUỐI CÙNG trong <c>AddRagStack</c>: mọi thứ ở đây đều là bọc quanh một đăng ký đã có
    /// sẵn, nên gọi sớm là bọc vào khoảng không và <c>Decorate</c> sẽ ném ngay lúc khởi động.
    /// </para>
    /// <para>
    /// Toàn bộ mối quan tâm "đo giờ" nằm gọn trong file này. Không một dòng nào của <c>AskPipeline</c>,
    /// của các module đăng ký khác, hay của bất kỳ interface nghiệp vụ nào phải thay đổi.
    /// </para>
    /// </summary>
    public static class LatencyServiceCollectionExtensions
    {
        public static IServiceCollection AddLatencyTracking(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(LatencyConfig.SectionName);

            services.AddValidatedOptions<LatencyConfig>(configuration, LatencyConfig.SectionName);

            // Đọc eager để CHỌN cài đặt, đúng cách AddQueryCache và AddSemanticAnswerCache đang làm.
            // Section vắng mặt thì Get<T>() trả null, nên rơi về mặc định của chính config class.
            var config = section.Get<LatencyConfig>() ?? new LatencyConfig();

            if (!config.Enabled)
                return services.AddDisabledLatencyTracking();

            services.AddSingleton<ILatencyReporter, LatencyLogReporter>();

            // Một instance duy nhất đứng sau hai interface: phiên đo và nơi ghi số liệu BẮT BUỘC
            // phải là cùng một object, nếu không thì decorator sẽ ghi vào một AsyncLocal khác với
            // cái mà phiên vừa mở. Cùng idiom với MemoryQueryCache và RedisSemanticAnswerCache.
            services.AddSingleton<AsyncLocalLatencyTracker>();
            services.AddSingleton<ILatencyTracker>(sp => sp.GetRequiredService<AsyncLocalLatencyTracker>());
            services.AddSingleton<ILatencySessionFactory>(sp => sp.GetRequiredService<AsyncLocalLatencyTracker>());

            return services.DecorateRagStack();
        }

        /// <summary>
        /// Tính năng tắt: chỉ đăng ký Null Object và KHÔNG bọc gì cả, nên chi phí đúng bằng không —
        /// không phải một nhánh <c>if</c> nằm trong từng lời gọi của tám interface.
        /// </summary>
        private static IServiceCollection AddDisabledLatencyTracking(this IServiceCollection services)
        {
            services.AddSingleton<NullLatencyTracker>();
            services.AddSingleton<ILatencyTracker>(sp => sp.GetRequiredService<NullLatencyTracker>());
            services.AddSingleton<ILatencySessionFactory>(sp => sp.GetRequiredService<NullLatencyTracker>());

            return services;
        }

        /// <summary>
        /// Thứ tự các dòng dưới đây KHÔNG quan trọng — mỗi dòng bọc một interface độc lập. Thứ tự
        /// trình bày theo đúng trình tự <c>AskPipeline</c> chạy, chỉ để đọc cho dễ đối chiếu.
        /// </summary>
        private static IServiceCollection DecorateRagStack(this IServiceCollection services)
        {
            // Gốc phiên: hai đường vào của hệ thống.
            services.Decorate<IAskService>((inner, sp) => new TimedAskService(
                inner,
                sp.GetRequiredService<ILatencyTracker>(),
                sp.GetRequiredService<ILatencySessionFactory>()));

            services.Decorate<IIngestionService>((inner, sp) => new TimedIngestionService(
                inner, sp.GetRequiredService<ILatencySessionFactory>()));

            // Các stage bên trong.
            services.Decorate<IQueryNormalizer>((inner, sp) => new TimedQueryNormalizer(
                inner, sp.GetRequiredService<ILatencyTracker>()));

            services.Decorate<ISemanticRouter>((inner, sp) => new TimedSemanticRouter(
                inner, sp.GetRequiredService<ILatencyTracker>()));

            services.Decorate<IWeakPointDetector>((inner, sp) => new TimedWeakPointDetector(
                inner, sp.GetRequiredService<ILatencyTracker>()));

            services.Decorate<IEmbeddingProvider>((inner, sp) => new TimedEmbeddingProvider(
                inner, sp.GetRequiredService<ILatencyTracker>()));

            services.Decorate<ISemanticAnswerCache>((inner, sp) => new TimedSemanticAnswerCache(
                inner, sp.GetRequiredService<ILatencyTracker>()));

            services.Decorate<IVectorStore>((inner, sp) => new TimedVectorStore(
                inner, sp.GetRequiredService<ILatencyTracker>()));

            // CHỈ đăng ký ILLMProvider không khóa. Lý do không bọc các provider keyed nằm ở
            // TimedLlmProvider — bọc cả hai là tính hai lần cùng một lượt gọi.
            services.Decorate<ILLMProvider>((inner, sp) => new TimedLlmProvider(
                inner, sp.GetRequiredService<ILatencyTracker>()));

            return services;
        }
    }
}
