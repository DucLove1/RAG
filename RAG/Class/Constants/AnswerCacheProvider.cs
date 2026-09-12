namespace RAG.Class.Constants
{
    /// <summary>
    /// Kho nào đứng sau tầng cache câu trả lời ngữ nghĩa. Dùng enum thay cho magic string để bind
    /// thẳng từ configuration và không sai chính tả được, cùng mẫu với
    /// <see cref="SemanticRouterStrategy"/>.
    /// <para>
    /// Cố ý KHÔNG phải khóa của Keyed Services, khác <see cref="LlmProviderKey"/>. Lý do là hai
    /// bài toán khác nhau về bản chất: cả hai <c>ILLMProvider</c> đều SỐNG CÙNG LÚC lúc chạy
    /// (router có thể dùng Gemini trong khi đường trả lời dùng Groq), còn ở đây chỉ đúng MỘT
    /// provider tồn tại. Đăng ký sẵn cả hai sẽ kéo theo ba hậu quả: options của Redis bị
    /// <c>ValidateOnStart</c> đánh giá và <c>[Required] ConnectionString</c> làm app không khởi
    /// động nổi khi đang chạy FAISS; <c>AddHostedService</c> vốn KHÔNG keyed được nên service ghi
    /// đĩa của FAISS vẫn chạy khi đang dùng Redis; và <c>RedisConnectionProvider</c> bị dựng dù
    /// không ai gọi. Vì vậy việc chọn nằm ở một <c>switch</c> trong composition root.
    /// </para>
    /// </summary>
    public enum AnswerCacheProvider
    {
        /// <summary>
        /// Redis + RediSearch. Cache sống NGOÀI tiến trình: nhiều instance dùng chung được, sống
        /// qua deploy mà không cần Disk. Đổi lại phải có một Redis 8+ có module search.
        /// </summary>
        Redis = 0,

        /// <summary>
        /// FAISS chạy TRONG tiến trình. Không phụ thuộc dịch vụ ngoài nào, lượt tra không đi qua
        /// mạng. Đổi lại cache nằm trong RAM của MỘT tiến trình: scale ra nhiều instance thì mỗi
        /// instance có một cache riêng, và tính bền vững phụ thuộc vào Disk gắn ở
        /// <c>SemanticAnswerCache:Faiss:PersistPath</c>.
        /// </summary>
        Faiss = 1
    }
}
