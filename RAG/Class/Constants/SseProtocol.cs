namespace RAG.Class.Constants
{
    /// <summary>
    /// Hằng khung dây của Server-Sent Events. Là từ khóa GIAO THỨC chứ không phải cấu hình, nên
    /// nằm ở đây thay vì trong appsettings — đổi chúng là nói một giao thức khác, không phải chỉnh
    /// một tham số.
    /// </summary>
    public static class SseProtocol
    {
        /// <summary>
        /// Kiểu nội dung. CỐ TÌNH không kèm <c>charset</c>: đặc tả SSE quy định thân luôn là UTF-8
        /// và yêu cầu client bỏ qua tham số charset, nên thêm vào chỉ là nhiễu.
        /// </summary>
        public const string ContentType = "text/event-stream";

        /// <summary>
        /// Thân SSE là MỘT response không giới hạn độ dài. Cache nào lưu nó lại sẽ phát lại nguyên
        /// một đoạn hội thoại cũ cho người chơi kế tiếp.
        /// </summary>
        public const string CacheControlNoCache = "no-cache";

        /// <summary>
        /// Cờ tắt đệm theo từng response của nginx.
        /// <para>
        /// Render đứng sau một reverse proxy họ nhà nginx, mà <c>proxy_buffering</c> BẬT theo mặc
        /// định. Cái đệm đó hoàn toàn VÔ HÌNH khi chạy localhost: stream chạy đẹp ở máy dev rồi lên
        /// production thì cả câu trả lời rơi xuống một lần, không một dòng lỗi nào.
        /// </para>
        /// </summary>
        public const string AccelBufferingHeader = "X-Accel-Buffering";

        public const string AccelBufferingOff = "no";

        public const string EventPrefix = "event: ";

        public const string DataPrefix = "data: ";

        /// <summary>Dòng chú thích của SSE. Mọi parser đúng chuẩn đều bỏ qua dòng bắt đầu bằng nó.</summary>
        public const string CommentPrefix = ":";

        /// <summary>
        /// Dấu hết luồng theo quy ước của OpenAI. Gemini hiện KHÔNG gửi nó, nhưng chịu được nó tốn
        /// đúng một phép so chuỗi và cứu ta khỏi một lần parse JSON ném ra giữa luồng nếu Google
        /// đổi ý.
        /// </summary>
        public const string DoneSentinel = "[DONE]";

        /// <summary>
        /// Kết thúc MỘT dòng trong khung.
        /// <para>
        /// Là hằng chứ không phải <c>Environment.NewLine</c>, và đó là chủ ý: <c>Environment.NewLine</c>
        /// cho ra "\r\n" trên Windows và "\n" trên Linux, nghĩa là CÙNG một server sẽ trả hai định
        /// dạng khác nhau tùy máy build. Đặc tả SSE chấp nhận cả ba kiểu xuống dòng nên không hỏng
        /// ngay, nhưng một client tự viết mà cắt chuỗi theo "\n" sẽ để lại "\r" ở cuối mỗi giá trị.
        /// </para>
        /// </summary>
        public const string LineEnd = "\n";

        /// <summary>Dòng trống kết thúc một khung, tức là dấu hiệu client được phép phát sự kiện.</summary>
        public const string FrameEnd = "\n\n";
    }
}
