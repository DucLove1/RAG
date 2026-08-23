using RAG.Class.Constants;

namespace RAG.Interface
{
    /// <summary>
    /// Trừu tượng hóa việc lấy <see cref="ILLMProvider"/> theo khóa.
    /// Nhờ đó các consumer không phụ thuộc trực tiếp vào IServiceProvider (DIP) và dễ mock khi test.
    /// </summary>
    public interface ILlmProviderResolver
    {
        ILLMProvider Resolve(LlmProviderKey key);
    }
}
