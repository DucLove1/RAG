namespace RAG.Interface
{
    /// <summary>
    /// Node phát hiện "trúng điểm yếu": hỏi LLM xem CÂU HỎI CỦA NGƯỜI CHƠI có trùng ý với một
    /// trong các câu chốt đã khai cho NPC đó hay không.
    /// <para>
    /// Không phải một route. Route là bộ lọc "trả lời thẳng cho rẻ"; đây là một sự kiện của cốt
    /// truyện — trúng thì pipeline THOÁT NGAY, không gọi LLM trả lời lần nào, và cờ được trả về
    /// cho client để nó chạy nhịp kịch bản của mình.
    /// </para>
    /// <para>
    /// Fail-open về <c>null</c> (KHÔNG trúng) như <see cref="ISemanticRouter"/>, nhưng hướng sai
    /// lệch ở đây đắt hơn hẳn: router đoán nhầm chỉ tốn một câu trả lời kém tự nhiên, còn báo
    /// trúng nhầm là lộ thủ phạm cho người chơi chưa suy luận ra. Vì vậy mọi thứ không phải "chắc
    /// chắn trúng" đều phải quy về không trúng.
    /// </para>
    /// </summary>
    public interface IWeakPointDetector
    {
        /// <param name="npcName">
        /// Tên NPC đang bị thẩm vấn. NPC không có mục trong cấu hình thì trả <c>null</c> mà KHÔNG
        /// gọi LLM — đây là thứ giữ chi phí của node ở mức một lượt gọi cho một NPC.
        /// </param>
        /// <param name="question">
        /// Câu hỏi ĐÃ CHUẨN HÓA — cùng lý do với <see cref="ISemanticRouter"/>, và các câu chốt
        /// trong cấu hình cũng được viết ở dạng chuẩn.
        /// </param>
        Task<WeakPointMatch?> DetectAsync(string npcName,
                                          string question,
                                          CancellationToken cancellationToken = default);
    }
}
