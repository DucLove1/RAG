namespace RAG.Interface
{
    /// <summary>
    /// Kết quả một lượt hỏi NPC. Trả thẳng ra controller như <see cref="IngestionResult"/>: hai
    /// trường nguyên thủy, không có gì để định dạng lại, nên một DTO riêng chỉ là lớp trung gian
    /// rỗng.
    /// </summary>
    /// <param name="WeakPointHit">
    /// <c>true</c> nghĩa là câu hỏi trúng điểm yếu của NPC. Khi đó <paramref name="Answer"/> là câu
    /// kịch bản lấy từ cấu hình chứ không phải câu do LLM sinh ra, và pipeline đã KHÔNG truy hồi,
    /// KHÔNG gọi LLM trả lời lần nào.
    /// </param>
    public sealed record AskResult(string Answer, bool WeakPointHit);
}
