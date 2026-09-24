namespace RAG.Interface
{
    /// <summary>
    /// Kết quả trích thực thể và ý định quan hệ từ một câu hỏi.
    /// </summary>
    /// <param name="Entities">
    /// Tên CHUẨN của các thực thể, đã đối chiếu với danh mục NPC được biết. Thứ tự là thứ tự LLM trả
    /// về và được dùng làm thứ hạng hạt giống.
    /// </param>
    /// <param name="RelationTypes">
    /// Loại quan hệ mà câu hỏi muốn biết, đã đối chiếu với ontology. Dùng để ƯU TIÊN cạnh, không lọc.
    /// </param>
    /// <param name="Failed">
    /// Bước trích hỏng (LLM lỗi, hết hạn, JSON hỏng, không lấy được danh mục). Khác hẳn "trích xong
    /// và không có thực thể nào": cờ này đi lên thành <c>Degraded</c> và chặn ghi cache câu trả lời.
    /// </param>
    public sealed record GraphExtraction(IReadOnlyList<string> Entities,
                                         IReadOnlyList<string> RelationTypes,
                                         bool Failed)
    {
        public static GraphExtraction Empty { get; } =
            new(Array.Empty<string>(), Array.Empty<string>(), Failed: false);

        public static GraphExtraction FailedResult { get; } =
            new(Array.Empty<string>(), Array.Empty<string>(), Failed: true);
    }

    /// <summary>
    /// Chọn hạt giống cho đồ thị từ câu hỏi: những thực thể câu hỏi nhắc tới, và những loại quan hệ
    /// nó muốn biết — chỉ trong phạm vi tri thức của NPC.
    /// <para>
    /// KHÔNG ném, trừ khi token của caller bị hủy. Sự cố trả <see cref="GraphExtraction.FailedResult"/>
    /// để câu trả lời tụt về RAG thuần chứ không về 500.
    /// </para>
    /// </summary>
    public interface IGraphEntityExtractor
    {
        Task<GraphExtraction> ExtractAsync(string npcName, string question, CancellationToken cancellationToken = default);
    }
}
