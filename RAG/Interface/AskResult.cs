namespace RAG.Interface
{
    /// <summary>
    /// Kết quả một lượt hỏi NPC. Trả thẳng ra controller như <see cref="IngestionResult"/>: hai
    /// trường nguyên thủy, không có gì để định dạng lại, nên một DTO riêng chỉ là lớp trung gian
    /// rỗng.
    /// </summary>
    /// <param name="WeakPointHit">
    /// Luôn <c>false</c>. Node phát hiện điểm yếu đã bị gỡ khỏi pipeline; trường này được giữ lại chỉ
    /// để client cũ đang đọc nó không bị vỡ.
    /// </param>
    public sealed record AskResult(string Answer, bool WeakPointHit);
}
