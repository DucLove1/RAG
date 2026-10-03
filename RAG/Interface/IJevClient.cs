using RAG.Class.Dto;

namespace RAG.Interface
{
    /// <summary>
    /// Client gọi mô hình quyết định Jev (TypeSafe System One). Tách khỏi router để router chỉ lo
    /// luật định tuyến, còn chuyện HTTP, xoay key và đọc phản hồi nằm ở đây.
    /// </summary>
    public interface IJevClient
    {
        /// <summary>Hỏi đúng một câu hỏi Choice về <paramref name="state"/> và trả về đáp án của nó.</summary>
        /// <exception cref="JevUnavailableException">
        /// Mọi trường hợp không lấy được đáp án dùng được: lỗi mạng, quá hạn, mọi key bị rate limit,
        /// mã lỗi khác 2xx, hoặc phản hồi thiếu đáp án cho <paramref name="questionId"/>.
        /// </exception>
        Task<SystemOneAnswer> AskChoiceAsync(string state,
                                             string questionId,
                                             SystemOneQuestion question,
                                             CancellationToken cancellationToken = default);
    }
}
