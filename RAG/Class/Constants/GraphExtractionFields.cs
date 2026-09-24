namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên trường trong đối tượng JSON mà LLM trích thực thể trả về.
    /// <para>
    /// Chỉ khai báo ở ĐÂY: prompt nhận hai tên này qua placeholder <c>{5}</c>/<c>{6}</c> chứ không gõ
    /// tay trong appsettings. Hai nơi gõ tay thì sớm muộn sẽ lệch một chữ, và bộ đọc lặng lẽ thấy
    /// đối tượng rỗng ở mọi câu hỏi.
    /// </para>
    /// </summary>
    public static class GraphExtractionFields
    {
        /// <summary>Danh sách tên thực thể, xếp theo mức độ trọng tâm giảm dần.</summary>
        public const string Entities = "thuc_the";

        /// <summary>Danh sách loại quan hệ mà câu hỏi muốn biết, xếp theo mức độ liên quan giảm dần.</summary>
        public const string RelationTypes = "quan_he";
    }

    /// <summary>
    /// Cú pháp JSON cần để cắt đối tượng ra khỏi đầu ra của LLM. Là HẰNG SỐ vì cùng lý do với
    /// <see cref="RouteLabelSyntax"/>: đây là cơ chế phân tích, không phải chính sách để vận hành đổi.
    /// </summary>
    public static class JsonSyntax
    {
        public const char ObjectStart = '{';

        public const char ObjectEnd = '}';
    }
}
