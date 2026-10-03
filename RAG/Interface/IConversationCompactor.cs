namespace RAG.Interface
{
    /// <summary>
    /// Gộp một đoạn lịch sử hội thoại (kèm tóm tắt cũ nếu có) thành MỘT chuỗi tóm tắt.
    /// <para>
    /// Không lưu gì: client gửi lên đoạn cần gộp, nhận về tóm tắt, rồi lần hỏi sau gửi tóm tắt đó
    /// kèm các lượt còn giữ nguyên văn. Client tự quyết định khi nào gọi.
    /// </para>
    /// </summary>
    public interface IConversationCompactor
    {
        /// <param name="history">
        /// Đoạn cần gộp. <see cref="ConversationHistory.Summary"/> là tóm tắt cũ, được gộp vào bản mới.
        /// </param>
        /// <exception cref="ConversationTooLongException">Số lượt vượt giới hạn cấu hình.</exception>
        Task<string> CompactAsync(ConversationHistory history, CancellationToken cancellationToken = default);
    }
}
