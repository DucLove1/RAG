namespace RAG.Interface
{
    /// <summary>
    /// Nạp trước vector câu mẫu cho node định tuyến.
    /// <para>
    /// Tồn tại để <c>SemanticRouterWarmupService</c> phụ thuộc vào abstraction thay vì vào lớp
    /// <c>EmbeddingSemanticRouter</c> cụ thể — trước đây service nền phải biết cả kiểu cụ thể lẫn
    /// một method <c>internal</c> của nó (DIP).
    /// </para>
    /// </summary>
    public interface IRouterWarmup
    {
        /// <summary>Trả về <c>true</c> khi có ít nhất một route dùng được sau khi nạp.</summary>
        Task<bool> TryBuildAsync(CancellationToken cancellationToken = default);
    }
}
