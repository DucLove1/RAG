namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên ba loại sự kiện của luồng trả lời. Đây là HỢP ĐỒNG với client game: đổi một chuỗi ở
    /// đây là phá client mà không có một lỗi biên dịch nào ở cả hai phía.
    /// <para>
    /// Chuỗi sự kiện bất biến: <see cref="Token"/>* → (<see cref="Done"/> | <see cref="Error"/>).
    /// Số token có thể bằng 0.
    /// </para>
    /// </summary>
    public static class AskStreamEventNames
    {
        /// <summary>Một mảnh văn bản. Client nối vào cuối phần đã nhận.</summary>
        public const string Token = "token";

        /// <summary>
        /// Lỗi xảy ra SAU khi luồng đã bắt đầu. Chỉ tồn tại vì lúc đó không còn đổi được mã HTTP
        /// nữa — lỗi TRƯỚC khi luồng bắt đầu vẫn là ProblemDetails với mã đúng, y hệt endpoint
        /// không streaming.
        /// </summary>
        public const string Error = "error";

        /// <summary>Kết thúc bình thường. Client nhận được nó thì biết câu trả lời đã trọn vẹn.</summary>
        public const string Done = "done";
    }
}
