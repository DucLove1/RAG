using RAG.Class.Constants;

namespace RAG.Extension
{
    /// <summary>
    /// Bóc phần có cấu trúc ra khỏi văn bản tự do mà LLM trả về.
    /// <para>
    /// Codebase chưa có JSON mode ở tầng provider, nên mô hình có thể bọc đối tượng trong code fence,
    /// thêm một câu dẫn phía trước, hay một dòng giải thích phía sau. Cắt từ dấu mở ĐẦU TIÊN tới dấu
    /// đóng CUỐI CÙNG xử lý được cả ba trường hợp mà không phải biết mô hình đã bọc kiểu gì.
    /// </para>
    /// </summary>
    public static class LlmOutputText
    {
        /// <returns>Đoạn từ <c>{</c> đầu tiên tới <c>}</c> cuối cùng, hoặc <c>null</c> nếu không có.</returns>
        public static string? ExtractJsonObject(string? output)
        {
            if (string.IsNullOrEmpty(output))
                return null;

            var start = output.IndexOf(JsonSyntax.ObjectStart);
            var end = output.LastIndexOf(JsonSyntax.ObjectEnd);

            return start >= 0 && end > start ? output[start..(end + 1)] : null;
        }
    }
}
