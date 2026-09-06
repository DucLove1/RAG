using RAG.Class.Config;
using RAG.Class.WeakPoint;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    public static class WeakPointServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký node phát hiện "trúng điểm yếu".
        /// <para>
        /// Chỉ một vai trò nên đăng ký thẳng, KHÔNG dùng mẫu <c>AddSingleton&lt;Concrete&gt;()</c>
        /// cộng factory như <c>AddLlmRouter</c>: mẫu đó tồn tại để tránh bẫy hai instance khi một
        /// kiểu cụ thể phục vụ hai interface, ở đây không có chuyện đó.
        /// </para>
        /// </summary>
        public static IServiceCollection AddWeakPoint(this IServiceCollection services,
                                                      IConfiguration configuration)
        {
            services.AddValidatedOptions<WeakPointConfig>(configuration, WeakPointConfig.SectionName);

            var config = configuration.GetSection(WeakPointConfig.SectionName).Get<WeakPointConfig>()
                         ?? new WeakPointConfig();

            // Bật nhưng không có target nào dùng được thì coi như tắt — cùng luật với ResolveStrategy
            // của router. Không có luật này, node sẽ gọi LLM với một danh sách câu chốt rỗng ở mọi
            // request tới mọi NPC, tốn tiền để luôn trả lời "không trúng".
            var hasUsableTarget = config.Targets.Any(target =>
                !string.IsNullOrWhiteSpace(target.NpcName) &&
                target.Triggers.Any(trigger => !string.IsNullOrWhiteSpace(trigger)));

            if (config.Enabled && hasUsableTarget)
                services.AddSingleton<IWeakPointDetector, LlmWeakPointDetector>();
            else
                services.AddSingleton<IWeakPointDetector, PassthroughWeakPointDetector>();

            return services;
        }
    }
}
