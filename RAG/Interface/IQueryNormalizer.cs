namespace RAG.Interface
{
    /// <summary>
    /// Node chuẩn hóa câu hỏi người dùng trước khi đưa vào embedding/prompt:
    /// mở rộng từ viết tắt, sửa lỗi chính tả, bổ sung dấu tiếng Việt.
    /// <para>
    /// Có lịch sử hội thoại thì còn viết lại câu nối tiếp thành câu ĐỘC LẬP ("thế còn hắn?" →
    /// "thế còn Johny thì sao?"). Làm ở đây chứ không ở từng node phía sau: định tuyến, điểm yếu,
    /// nhúng và truy hồi đều chỉ nhìn câu đã chuẩn hóa, nên viết lại một lần là cả chuỗi đúng theo.
    /// </para>
    /// </summary>
    public interface IQueryNormalizer
    {
        /// <param name="history">Lịch sử hội thoại; <see cref="ConversationHistory.Empty"/> khi không có.</param>
        Task<string> NormalizeAsync(string question,
                                    ConversationHistory history,
                                    CancellationToken cancellationToken = default);
    }
}
