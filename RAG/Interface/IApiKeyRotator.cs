namespace RAG.Interface
{
    /// <summary>
    /// Luân chuyển qua các API key khi gặp rate limit (429). Một rotator per pool API key.
    /// </summary>
    public interface IApiKeyRotator
    {
        /// <summary>
        /// Lấy API key hiện tại. Quét qua các key không bị giới hạn để tìm cái đang dùng.
        /// </summary>
        /// <exception cref="AllApiKeysRateLimitedException">Mọi key trong pool đều bị giới hạn.</exception>
        string GetCurrentKey();

        /// <summary>
        /// Báo cáo key bị giới hạn tần suất (429). Đánh dấu key đó và chuyển sang key tiếp theo.
        /// Nếu key đã bị đánh dấu rồi, không làm gì cả (idempotent).
        /// </summary>
        void ReportRateLimited(string key);
    }
}
