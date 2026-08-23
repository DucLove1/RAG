using RAG.Interface;

namespace RAG.Class.Normalization
{
    /// <summary>
    /// Null Object cho <see cref="IQueryNormalizer"/>: trả về nguyên câu hỏi gốc.
    /// Được đăng ký khi node chuẩn hóa bị tắt, nhờ đó pipeline không cần biết đến cờ Enabled.
    /// </summary>
    public class PassthroughQueryNormalizer : IQueryNormalizer
    {
        public Task<string> NormalizeAsync(string question, CancellationToken cancellationToken = default) =>
            Task.FromResult(question);
    }
}
