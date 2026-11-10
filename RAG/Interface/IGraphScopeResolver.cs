namespace RAG.Interface
{
    /// <summary>
    /// Phạm vi tri thức của MỘT lượt tra đồ thị: NPC nào, đang ở stage nào, và vì thế những dòng
    /// corpus nào CHƯA được hé lộ.
    /// <para>
    /// Đồ thị chỉ lưu mã chunk, không lưu stage. Nên "chưa hé lộ" phải đi vào Cypher dưới dạng một
    /// TẬP MÃ CHUNK, và tập đó phải là MỘT giá trị dùng chung cho cả ba chỗ lọc: danh mục thực thể
    /// đưa cho LLM, truy vấn mở rộng, và bước tra nguyên văn. Tính lại ở từng chỗ thì chỉ cần một
    /// chỗ đọc corpus ở thời điểm khác là ba chỗ lọc theo ba luật khác nhau.
    /// </para>
    /// </summary>
    /// <param name="Stage">
    /// Tên stage của request, hoặc <c>null</c> khi client không gửi — khi đó không lọc gì, y hệt
    /// trước khi có tính năng này. Đồng thời là một phần khóa cache danh mục.
    /// </param>
    /// <param name="HiddenChunkCodes">Mã của những dòng mang stage SAU stage hiện tại.</param>
    public sealed record GraphScope(string NpcName, string? Stage, IReadOnlySet<string> HiddenChunkCodes)
    {
        private static readonly IReadOnlySet<string> Nothing = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Không lọc theo stage: request không gửi stage, hoặc đồ thị đang tắt.</summary>
        public static GraphScope Unrestricted(string npcName) => new(npcName, null, Nothing);

        public bool IsHidden(string chunkCode) => HiddenChunkCodes.Contains(chunkCode);
    }

    /// <summary>
    /// Dựng <see cref="GraphScope"/> cho một request.
    /// <para>
    /// KHÔNG ném, trừ khi token của caller bị hủy. Trả <c>null</c> khi request CÓ gửi stage mà không
    /// xác định được dòng nào thuộc stage nào (vd. không đọc được corpus). Caller phải coi đó là suy
    /// biến chứ không được tra tiếp không lọc: tra tiếp là để lộ tri thức của màn sau đúng lúc hệ
    /// thống đang trục trặc — lúc ít người nhìn nhất.
    /// </para>
    /// </summary>
    public interface IGraphScopeResolver
    {
        Task<GraphScope?> ResolveAsync(string npcName, string? stage, CancellationToken cancellationToken = default);
    }
}
