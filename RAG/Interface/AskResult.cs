namespace RAG.Interface
{
    /// <summary>
    /// Kết quả một lượt hỏi NPC. Trả thẳng ra controller như <see cref="IngestionResult"/>: một
    /// trường nguyên thủy, không có gì để định dạng lại, nên một DTO riêng chỉ là lớp trung gian
    /// rỗng.
    /// </summary>
    public sealed record AskResult(string Answer);
}
