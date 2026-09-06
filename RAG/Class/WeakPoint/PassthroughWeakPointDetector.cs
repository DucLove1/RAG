using RAG.Interface;

namespace RAG.Class.WeakPoint
{
    /// <summary>
    /// Null Object: không câu nào trúng điểm yếu. Được đăng ký khi <c>WeakPoint:Enabled = false</c>
    /// hoặc khi không khai target nào dùng được, nhờ đó pipeline không cần biết tới cờ bật/tắt và
    /// không có nhánh <c>if (Enabled)</c> nào phải nuôi.
    /// </summary>
    public sealed class PassthroughWeakPointDetector : IWeakPointDetector
    {
        public Task<WeakPointMatch?> DetectAsync(string npcName,
                                                 string question,
                                                 CancellationToken cancellationToken = default) =>
            Task.FromResult<WeakPointMatch?>(null);
    }
}
