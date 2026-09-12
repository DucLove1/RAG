namespace RAG.Class.Constants
{
    /// <summary>
    /// Các hằng số thuộc về giao thức của Google Generative Language API.
    /// Đây là ràng buộc của nhà cung cấp (không phải cấu hình người dùng) nên khai báo dưới dạng hằng.
    /// </summary>
    public static class GeminiApiDefaults
    {
        public const string ApiKeyHeader = "x-goog-api-key";
        public const string UserRole = "user";

        /// <summary>
        /// Tham số bắt buộc của endpoint streaming.
        /// <para>
        /// Thiếu nó, Google KHÔNG trả về SSE mà trả một MẢNG JSON được stream dần. Bộ đọc theo
        /// dòng sẽ không khớp một dòng nào, và triệu chứng là câu trả lời RỖNG chứ không phải một
        /// lỗi — không exception, không dòng log nào để lần ra.
        /// </para>
        /// </summary>
        public const string SseAltQuery = "alt=sse";
    }
}
