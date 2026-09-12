namespace RAG.Interface
{
    /// <summary>
    /// Một sự kiện trong luồng trả lời.
    /// <para>
    /// Cây record kín chứ không phải một record phẳng có trường <c>Type</c>: phẳng thì mọi trường
    /// của mọi loại sự kiện phải cùng tồn tại và nullable, và trình biên dịch không còn giúp được
    /// gì khi thêm loại thứ năm. Với cây kín, một <c>switch</c> thiếu nhánh là một cảnh báo ngay
    /// lúc build.
    /// </para>
    /// <para>
    /// Đây là kiểu của TẦNG ỨNG DỤNG, không phải kiểu trên dây. Việc nó được serialize thành JSON
    /// nào và gói vào khung SSE nào là chuyện của <c>AskStreamSseResult</c> — nhờ vậy
    /// <c>AskStreamPipeline</c> không biết gì về HTTP.
    /// </para>
    /// </summary>
    public abstract record AskStreamEvent;

    /// <summary>
    /// Siêu dữ liệu của lượt trả lời. Luôn đi TRƯỚC mọi <see cref="AskStreamTokenEvent"/>.
    /// </summary>
    /// <param name="WeakPointHit">
    /// Câu hỏi có trúng điểm yếu của NPC hay không. Cùng ngữ nghĩa với trường cùng tên của
    /// <see cref="AskResult"/>, và cố tình giữ nguyên tên để client chuyển từ <c>ask</c> sang
    /// <c>ask-stream</c> không phải đổi cách hiểu.
    /// </param>
    public sealed record AskStreamMetaEvent(bool WeakPointHit) : AskStreamEvent;

    /// <summary>
    /// Một mảnh văn bản.
    /// <para>
    /// KHÔNG bảo đảm mảnh trùng với ranh giới từ hay câu — nó là delta của model, có thể là một
    /// ký tự lẻ hoặc cả một đoạn. Client chỉ được phép nối vào cuối, không được suy diễn gì thêm.
    /// </para>
    /// </summary>
    public sealed record AskStreamTokenEvent(string Text) : AskStreamEvent;

    /// <summary>
    /// Lỗi xảy ra sau khi luồng đã bắt đầu.
    /// <para>
    /// CHỈ do tầng ghi ra dây sinh, không bao giờ do <c>IAskStreamService</c> sinh: nó mang mã HTTP,
    /// mà mã HTTP là khái niệm của tầng vận chuyển.
    /// </para>
    /// </summary>
    /// <param name="Title">
    /// Câu chữ lấy từ <c>ErrorResponses</c>, ĐÚNG chuỗi mà endpoint không streaming trả trong
    /// ProblemDetails cho cùng nguyên nhân. Người chơi không được thấy hai câu khác nhau cho cùng
    /// một sự cố chỉ vì nó xảy ra trước hay sau token đầu. Tuyệt đối KHÔNG mang stack trace.
    /// </param>
    public sealed record AskStreamErrorEvent(int Status, string Title) : AskStreamEvent;

    /// <summary>
    /// Kết thúc bình thường. Rỗng về nội dung — nó chỉ có nghĩa "không còn byte nào nữa", và cũng
    /// chỉ do tầng ghi ra dây sinh.
    /// </summary>
    public sealed record AskStreamDoneEvent : AskStreamEvent;
}
