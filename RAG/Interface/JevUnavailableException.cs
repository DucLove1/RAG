namespace RAG.Interface
{
    /// <summary>
    /// Không lấy được đáp án dùng được từ Jev. Router bắt lỗi này và fail-open về đường RAG, nên nó
    /// không bao giờ trở thành lỗi của request.
    /// </summary>
    public sealed class JevUnavailableException : Exception
    {
        public JevUnavailableException(string message) : base(message) { }

        public JevUnavailableException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
