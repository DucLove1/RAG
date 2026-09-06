namespace RAG.Interface
{
    /// <summary>
    /// Kết quả khi câu hỏi của người chơi trúng điểm yếu của NPC.
    /// <para>
    /// KHÔNG mang lại tên NPC: caller vừa truyền nó vào, echo ngược lại chỉ tạo thêm một chỗ để hai
    /// giá trị lệch nhau. Khác <see cref="RouteMatch"/>, ở đó tên route là thứ caller chưa biết.
    /// </para>
    /// <para>
    /// Là một record chứ không phải <c>string?</c> để "trúng" trở thành một KIỂU: chuỗi rỗng và
    /// "không trúng" là hai chuyện khác hẳn nhau, và lời thoại được phép để trống.
    /// </para>
    /// </summary>
    /// <param name="Reply">
    /// Câu NPC nói khi bị bắt bài, lấy nguyên văn từ cấu hình — CỐ TÌNH không qua LLM: đây là một
    /// nhịp kịch bản, không phải một câu trả lời được sinh ra.
    /// </param>
    public sealed record WeakPointMatch(string Reply);
}
