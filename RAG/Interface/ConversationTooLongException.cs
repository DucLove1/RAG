namespace RAG.Interface
{
    /// <summary>
    /// Đoạn lịch sử gửi lên để compact dài hơn giới hạn cấu hình. Là lỗi của request (400), không
    /// phải của server: client cần compact sớm hơn, hoặc gửi theo từng phần kèm tóm tắt cũ.
    /// </summary>
    public sealed class ConversationTooLongException : Exception
    {
        public ConversationTooLongException(string message) : base(message) { }
    }
}
