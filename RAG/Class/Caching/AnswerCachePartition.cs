using System.Security.Cryptography;
using System.Text;
using RAG.Class.Constants;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Định nghĩa DUY NHẤT của việc "một entry cache thuộc về ai" và "một entry cache là câu nào".
    /// Dùng chung cho mọi provider.
    /// <para>
    /// Gom về một chỗ là điều kiện để đổi provider mà không đổi ngữ nghĩa: nếu Redis băm theo một
    /// cách và FAISS băm theo cách khác thì hai bên phân vùng khác nhau, và bài kiểm chứng "cùng
    /// một danh sách câu hỏi phải cho cùng một tập kết quả" mất hết ý nghĩa. Tệ hơn, một khác biệt
    /// nhỏ trong cách chuẩn hóa (quên <c>Trim</c>, quên <c>ToLowerInvariant</c>) sẽ chỉ lộ ra dưới
    /// dạng "cache tự nhiên nguội" ở đúng một provider.
    /// </para>
    /// </summary>
    public static class AnswerCachePartition
    {
        /// <summary>
        /// Giá trị phân vùng: băm của (tên NPC + mô tả tính cách).
        /// <para>
        /// Mô tả tính cách PHẢI nằm trong phân vùng vì nó do CLIENT gửi lên theo từng request và đi
        /// thẳng vào system prompt — cùng tên NPC với hai persona khác nhau là hai câu trả lời khác
        /// nhau. Gộp vào một giá trị băm thay vì hai trường riêng: một thứ ít hơn để đồng bộ giữa
        /// đường đọc và đường ghi.
        /// </para>
        /// </summary>
        public static string Tag(string npcName, string npcPersona) => Hash(npcName + ' ' + npcPersona);

        /// <summary>
        /// Danh tính của một câu hỏi TRONG một phân vùng. Đây là thứ làm cho việc hỏi lại ĐÚNG câu
        /// cũ ghi đè entry cũ thay vì đẻ thêm entry mới.
        /// <para>
        /// Không dùng GUID (mỗi lần trượt đẻ một entry, tích lại thành hàng nghìn vector gần trùng
        /// làm chậm KNN mà không tăng recall) và không băm vector (float không phải danh tính ổn
        /// định — cùng câu nhúng lại có thể lệch bit cuối, lại đẻ entry mới).
        /// </para>
        /// </summary>
        public static string QuestionHash(string question) => Hash(question);

        /// <summary>
        /// Băm một giá trị thành hex có độ dài cố định.
        /// <para>
        /// Ra hex chứ không giữ nguyên chuỗi vì cú pháp TAG của RediSearch coi khoảng trắng và gần
        /// như toàn bộ dấu câu là ký tự đặc biệt phải escape, còn DẤU PHẨY thì bị hiểu là dấu ngăn
        /// giữa hai tag. Tên NPC trong game có cả khoảng trắng lẫn dấu tiếng Việt, nên đi đường
        /// escape nghĩa là phải chép đúng một bảng escape của RediSearch và giữ nó đồng bộ qua các
        /// phiên bản. Băm ra [0-9A-F] thì không còn ký tự nào cần escape và bảng escape đó biến mất
        /// khỏi codebase. Provider FAISS không cần điều này, nhưng dùng chung một hàm thì hai bên
        /// phân vùng giống hệt nhau — thứ đáng giá hơn nhiều.
        /// </para>
        /// <para>
        /// Chuẩn hóa Trim + ToLowerInvariant TRƯỚC khi băm: "Johny" và "johny " phải là cùng một
        /// NPC, và quyết định đó nên nằm ở đây một cách cố ý thay vì phụ thuộc vào việc client gửi
        /// lên thế nào.
        /// </para>
        /// </summary>
        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())))
                   [..AnswerCacheFields.TagLength];
    }
}
