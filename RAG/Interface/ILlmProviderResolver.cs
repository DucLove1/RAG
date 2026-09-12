using RAG.Class.Constants;

namespace RAG.Interface
{
    /// <summary>
    /// Trừu tượng hóa việc lấy nhà cung cấp LLM theo khóa.
    /// Nhờ đó các consumer không phụ thuộc trực tiếp vào IServiceProvider (DIP) và dễ mock khi test.
    /// </summary>
    public interface ILlmProviderResolver
    {
        ILLMProvider Resolve(LlmProviderKey key);

        /// <summary>
        /// Bản streaming. Cùng một khóa trỏ về CÙNG một instance với <see cref="Resolve"/> — chỉ
        /// là nhìn qua một vai trò khác. Quan trọng vì provider giữ trạng thái dùng chung (cache
        /// đối tượng client, và qua rotator là cả trạng thái giới hạn tần suất của từng key): hai
        /// instance rời nhau nghĩa là hai đường hỏi đáp đếm cooldown riêng, và một key vừa bị 429
        /// ở đường này vẫn được đường kia dùng tiếp.
        /// </summary>
        ILLMStreamProvider ResolveStream(LlmProviderKey key);
    }
}
