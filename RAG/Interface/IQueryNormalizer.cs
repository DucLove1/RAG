namespace RAG.Interface
{
    /// <summary>
    /// Node chuẩn hóa câu hỏi người dùng trước khi đưa vào embedding/prompt:
    /// mở rộng từ viết tắt, sửa lỗi chính tả, bổ sung dấu tiếng Việt.
    /// </summary>
    public interface IQueryNormalizer
    {
        Task<string> NormalizeAsync(string question, CancellationToken cancellationToken = default);
    }
}
