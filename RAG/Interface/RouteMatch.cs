namespace RAG.Interface
{
    /// <summary>
    /// Kết quả khi một route "trả lời thẳng" được chọn.
    /// Chỉ mang template chứ không mang chuỗi đã render, để pipeline giữ đúng vai trò dựng prompt
    /// và bản thân đường trả lời không bao giờ phải nhìn thấy "{0}".
    /// </summary>
    /// <param name="Name">Tên route, dùng cho log và chẩn đoán.</param>
    /// <param name="Score">
    /// Điểm cosine cao nhất của route này, hoặc <c>null</c> khi quyết định KHÔNG dựa trên điểm số
    /// (chiến lược LLM chọn nhãn, không chấm điểm).
    /// <para>
    /// Cố tình để <c>null</c> chứ không nhét một giá trị canh sẵn kiểu 1.0: một điểm số bịa ra sẽ
    /// lặng lẽ vượt qua mọi phép so ngưỡng mà ai đó viết về sau, còn một điểm số vắng mặt thì không.
    /// </para>
    /// </param>
    /// <param name="SystemPromptTemplate">{0} = tên NPC, {1} = tính cách NPC.</param>
    /// <param name="UserPromptTemplate">{0} = câu hỏi đã chuẩn hóa.</param>
    public sealed record RouteMatch(
        string Name,
        double? Score,
        string SystemPromptTemplate,
        string UserPromptTemplate)
    {
        public string BuildSystemPrompt(string npcName, string npcPersonality) =>
            string.Format(SystemPromptTemplate, npcName, npcPersonality);

        public string BuildUserPrompt(string question) =>
            string.Format(UserPromptTemplate, question);
    }

    /// <summary>
    /// Đánh giá của một route đối với một câu hỏi. Chỉ phục vụ endpoint chẩn đoán, không tham gia
    /// vào luồng trả lời.
    /// </summary>
    /// <param name="Score">Điểm cosine, hoặc <c>null</c> với chiến lược không chấm điểm.</param>
    /// <param name="Threshold">Ngưỡng của route, hoặc <c>null</c> với chiến lược không có ngưỡng.</param>
    public sealed record RouteScore(string Name, double? Score, double? Threshold, bool Matched);
}
