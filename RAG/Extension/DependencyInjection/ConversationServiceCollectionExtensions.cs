using RAG.Class.Config;
using RAG.Class.Conversation;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký phần lịch sử hội thoại: bộ dựng lịch sử thành văn bản và endpoint compact.</summary>
    public static class ConversationServiceCollectionExtensions
    {
        public static IServiceCollection AddConversation(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatedOptions<ConversationConfig>(configuration, ConversationConfig.SectionName);

            services.AddSingleton<IConversationFormatter, ConversationFormatter>();
            services.AddSingleton<IConversationCompactor, LlmConversationCompactor>();

            return services;
        }
    }
}
