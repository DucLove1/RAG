namespace RAG.Interface
{
    public interface ILLMProvider
    {
        /// <summary>
        /// <paramref name="model"/> để trống thì provider dùng model mặc định của chính nó
        /// (cấu hình Model trong section provider). Cho phép mỗi consumer (chuẩn hóa, router...)
        /// chọn model riêng mà không cần thêm provider hay pool API key mới.
        /// </summary>
        Task<string> AskAsync(string system, string user, string? model = null, CancellationToken cancellationToken = default);
    }
}
