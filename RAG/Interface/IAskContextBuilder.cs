namespace RAG.Interface
{
    /// <summary>
    /// Ngữ cảnh đã dựng sẵn cho một câu hỏi, tách làm hai khối để prompt đặt chúng riêng.
    /// </summary>
    /// <param name="Text">Nguyên văn: các đoạn khớp vector, rồi các dòng mà đồ thị trỏ tới.</param>
    /// <param name="Graph">Khối mối liên hệ đã render. Rỗng khi đồ thị tắt hoặc không có cạnh nào.</param>
    /// <param name="GraphDegraded">
    /// Đồ thị bật nhưng lượt tra hỏng. Caller phải chặn ghi cache khi cờ này bật — xem
    /// <see cref="GraphContext.Degraded"/>.
    /// </param>
    public sealed record AskContext(string Text,
                                    string Graph,
                                    int HitCount,
                                    int ExtraTextCount,
                                    int RelationCount,
                                    bool GraphDegraded)
    {
        /// <summary>Có gì để trả lời dựa vào không — cùng nghĩa với <c>hits.Count &gt; 0</c> của bản trước.</summary>
        public bool HasContext => HitCount > 0 || RelationCount > 0;
    }

    /// <summary>
    /// Dựng ngữ cảnh truy hồi cho đường trả lời: nhánh vector và nhánh đồ thị (trích thực thể → mở
    /// rộng → tra nguyên văn) chạy SONG SONG, rồi gộp và chia ngân sách.
    /// <para>
    /// Là điểm gộp DUY NHẤT của hai nhánh, nên đổi cách truy hồi không bao giờ phải sửa pipeline.
    /// </para>
    /// <para>
    /// Tồn tại để khối truy hồi không bị chép ở cả <c>AskPipeline</c> lẫn <c>AskStreamPipeline</c>.
    /// Bước NHÚNG cố ý ở lại trong từng pipeline: vị trí của nó so với nhánh định tuyến là một
    /// bất biến của pipeline, không phải của việc dựng ngữ cảnh.
    /// </para>
    /// </summary>
    public interface IAskContextBuilder
    {
        /// <param name="stage">
        /// Giai đoạn mạch chuyện hiện tại của NPC, do client tự xác định và gửi lên; <c>null</c>
        /// hoặc rỗng thì KHÔNG lọc theo stage — trả về như trước khi có tính năng này. Có giá trị
        /// thì chỉ lấy những chunk không gắn stage HOẶC gắn đúng stage đó.
        /// </param>
        Task<AskContext> BuildAsync(string npcName,
                                    string question,
                                    float[] questionEmbedding,
                                    int topK,
                                    string? stage = null,
                                    CancellationToken cancellationToken = default);
    }
}
