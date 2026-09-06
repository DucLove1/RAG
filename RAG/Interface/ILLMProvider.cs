namespace RAG.Interface
{
    public interface ILLMProvider
    {
        /// <summary>
        /// Trần token đầu ra mà provider này thực sự gửi lên API. Lộ ra ở đây để tầng dựng prompt
        /// nhắm đúng con số API sẽ cắt, thay vì giữ một bản sao riêng rồi lệch dần theo thời gian.
        /// </summary>
        int MaxOutputTokens { get; }

        /// <summary>
        /// <paramref name="model"/> để trống thì provider dùng model mặc định của chính nó
        /// (cấu hình Model trong section provider). Cho phép mỗi consumer (chuẩn hóa, router...)
        /// chọn model riêng mà không cần thêm provider hay pool API key mới.
        /// </summary>
        Task<string> AskAsync(string system, string user, string? model = null, CancellationToken cancellationToken = default);
    }
}
