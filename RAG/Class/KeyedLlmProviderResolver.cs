using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class
{
    /// <summary>
    /// Cài đặt <see cref="ILlmProviderResolver"/> dựa trên Keyed Services của .NET DI.
    /// Đây là nơi duy nhất chạm tới service locator, phần còn lại của ứng dụng vẫn thuần DI.
    /// </summary>
    public class KeyedLlmProviderResolver : ILlmProviderResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public KeyedLlmProviderResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ILLMProvider Resolve(LlmProviderKey key) =>
            _serviceProvider.GetRequiredKeyedService<ILLMProvider>(key);
    }
}
