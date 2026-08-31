namespace RAG.Interface
{
    /// <summary>
    /// Giải thích quyết định định tuyến cho một câu hỏi. Chỉ phục vụ endpoint chẩn đoán khi tinh
    /// chỉnh route, không nằm trong đường trả lời của người chơi.
    /// <para>
    /// Tách khỏi <see cref="ISemanticRouter"/> theo ISP: <c>AskPipeline</c> chỉ cần biết route nào
    /// khớp, không cần biết điểm của những route không khớp.
    /// </para>
    /// <para>
    /// Trả về TRỌN <see cref="RouteExplanation"/> — cả điểm lẫn route thắng — trong một lần gọi.
    /// Bản trước tách thành hai method (<c>Explain</c> rồi <c>Route</c>) nên với chiến lược LLM,
    /// một lần chẩn đoán sẽ tốn HAI lượt gọi mô hình cho cùng một câu.
    /// </para>
    /// </summary>
    public interface IRouteExplainer
    {
        /// <param name="normalizedQuestion">Câu hỏi đã qua bước chuẩn hóa.</param>
        Task<RouteExplanation> ExplainAsync(string normalizedQuestion, CancellationToken cancellationToken = default);
    }
}
