namespace RAG.Class.Constants
{
    /// <summary>
    /// Cú pháp cần bóc khỏi đầu ra của LLM khi nó trả về nhãn route.
    /// <para>
    /// Cố tình là HẰNG SỐ chứ không phải configuration, dù dự án có quy ước "không hardcode".
    /// Quy ước đó nhắm vào chính sách — câu chữ, ngưỡng, template — những thứ người vận hành
    /// có lý do để đổi. Còn đây là cơ chế phân tích: nó mô tả cách các mô hình ngôn ngữ hay
    /// bọc câu trả lời lại (code fence, dấu nháy, tiền tố "Nhãn:"). Đưa xuống JSON chỉ tạo ra
    /// một cách để làm hỏng bộ phân tích từ file cấu hình, không tạo ra khả năng nào mới.
    /// </para>
    /// </summary>
    public static class RouteLabelSyntax
    {
        /// <summary>Dấu mở/đóng khối code mà model hay bọc quanh câu trả lời.</summary>
        public const string CodeFence = "```";

        /// <summary>Các info string hay đi kèm sau dấu mở khối code.</summary>
        public static readonly string[] CodeFenceInfoStrings = { "json", "text", "plaintext", "txt" };

        /// <summary>
        /// Dấu ngăn giữa tiền tố và nhãn ("Nhãn: chitchat", "- chitchat", "Kết quả — chitchat").
        /// Bộ phân tích cắt tại dấu CUỐI CÙNG, vì tên route không bao giờ chứa chúng.
        /// </summary>
        public static readonly char[] LabelSeparators = { ':', '-', '—', '–', '>' };

        /// <summary>Dấu nháy và dấu nhấn markdown cần gỡ ở hai đầu.</summary>
        public static readonly char[] Quotes = { '"', '\'', '`', '*', '_', '“', '”', '‘', '’' };

        /// <summary>Dấu câu cần gỡ ở cuối nhãn.</summary>
        public static readonly char[] TrailingPunctuation = { '.', ',', '!', '?', ';', ':' };

        /// <summary>
        /// Các ký tự được coi là dấu ngăn từ trong tên route; đều được quy về
        /// <see cref="WordSeparator"/> để "chit chat", "chit-chat" và "chit_chat" cùng tra được.
        /// </summary>
        public static readonly char[] WordSeparators = { ' ', '\t', '-', '.', '/' };

        public const char WordSeparator = '_';
    }
}
