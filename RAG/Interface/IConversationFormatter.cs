namespace RAG.Interface
{
    /// <summary>
    /// Nơi DUY NHẤT biết cách cắt gọn và dựng lịch sử hội thoại thành văn bản. Bộ chuẩn hóa, prompt
    /// trả lời và endpoint compact cùng dùng nó, để ba nơi nhìn lịch sử theo cùng một cách.
    /// </summary>
    public interface IConversationFormatter
    {
        /// <summary>Giới hạn cho prompt trả lời: giữ số lượt cuối, cắt lượt và tóm tắt quá dài.</summary>
        ConversationHistory Trim(ConversationHistory history);

        /// <summary>
        /// Chỉ cắt lượt và tóm tắt quá dài, GIỮ đủ số lượt — dành cho endpoint compact, nơi gộp chính
        /// những lượt cũ mà <see cref="Trim"/> sẽ bỏ đi.
        /// </summary>
        ConversationHistory TrimLengths(ConversationHistory history);

        /// <summary>Bản ghi trần: dòng tóm tắt (nếu có) rồi từng lượt. Rỗng khi lịch sử rỗng.</summary>
        string RenderTranscript(ConversationHistory history);

        /// <summary>
        /// Gắn khối lịch sử lên đầu user prompt trả lời. Lịch sử rỗng thì trả lại
        /// <paramref name="userPrompt"/> nguyên vẹn — prompt giống hệt bản chưa có tính năng này.
        /// </summary>
        string PrependToAnswerPrompt(ConversationHistory history, string userPrompt);

        /// <summary>Cắt một chuỗi tóm tắt theo giới hạn cấu hình.</summary>
        string TrimSummary(string summary);
    }
}
