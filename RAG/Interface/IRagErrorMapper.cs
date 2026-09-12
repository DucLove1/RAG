namespace RAG.Interface
{
    /// <summary>
    /// Ánh xạ exception của tầng nghiệp vụ sang cặp (mã HTTP, tiêu đề hiển thị).
    /// <para>
    /// Tách khỏi bộ xử lý exception vì giờ có HAI nơi cần đúng phép ánh xạ này: bộ xử lý ghi
    /// ProblemDetails khi response CHƯA bắt đầu, còn tầng SSE ghi một sự kiện lỗi khi nó ĐÃ bắt
    /// đầu — lúc đó mã HTTP không sửa được nữa nên phải đưa vào thân.
    /// </para>
    /// <para>
    /// Hai bản sao của cùng phép ánh xạ là hai bộ tiêu đề sẽ lệch nhau theo thời gian, và triệu
    /// chứng là người chơi thấy hai câu chữ khác nhau cho CÙNG một sự cố, chỉ khác ở chỗ nó xảy ra
    /// trước hay sau token đầu tiên.
    /// </para>
    /// </summary>
    public interface IRagErrorMapper
    {
        (int Status, string Title) Map(Exception exception);
    }
}
