namespace RAG.Class.Constants
{
    /// <summary>
    /// Các hằng số thuộc về giao thức của Google Generative Language API.
    /// Đây là ràng buộc của nhà cung cấp (không phải cấu hình người dùng) nên khai báo dưới dạng hằng.
    /// </summary>
    public static class GeminiApiDefaults
    {
        public const string ApiKeyHeader = "x-goog-api-key";

        /// <summary>Loại step chứa câu trả lời của model trong <c>steps[]</c> (các loại khác: thought, user_input...).</summary>
        public const string ModelOutputStepType = "model_output";

        /// <summary>Loại nội dung văn bản, dùng cho cả <c>steps[].content[].type</c> lẫn <c>delta.type</c>.</summary>
        public const string TextContentType = "text";

        /// <summary>Sự kiện SSE mang một mảnh nội dung mới.</summary>
        public const string StepDeltaEventType = "step.delta";

        /// <summary>Sự kiện SSE báo lỗi giữa luồng — phải ném ra, nếu không luồng kết thúc im lặng với câu trả lời cụt.</summary>
        public const string ErrorEventType = "error";

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
