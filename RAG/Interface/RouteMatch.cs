namespace RAG.Interface
{
    /// <summary>
    /// Kết quả khi một route "trả lời thẳng" thắng ngưỡng tương đồng.
    /// Chỉ mang template chứ không mang chuỗi đã render, để pipeline giữ đúng vai trò dựng prompt
    /// và bản thân RAGPipline không bao giờ phải nhìn thấy "{0}".
    /// </summary>
    /// <param name="Name">Tên route, dùng cho log và chẩn đoán.</param>
    /// <param name="Score">Điểm cosine cao nhất của route này.</param>
    /// <param name="SystemPromptTemplate">{0} = tên NPC, {1} = tính cách NPC.</param>
    /// <param name="UserPromptTemplate">{0} = câu hỏi đã chuẩn hóa.</param>
    public sealed record RouteMatch(
        string Name,
        double Score,
        string SystemPromptTemplate,
        string UserPromptTemplate)
    {
        public string BuildSystemPrompt(string npcName, string npcPersonality) =>
            string.Format(SystemPromptTemplate, npcName, npcPersonality);

        public string BuildUserPrompt(string question) =>
            string.Format(UserPromptTemplate, question);
    }

    /// <summary>
    /// Điểm của một route đối với một câu hỏi. Chỉ phục vụ endpoint chẩn đoán khi tinh chỉnh ngưỡng,
    /// không tham gia vào luồng trả lời.
    /// </summary>
    public sealed record RouteScore(string Name, double Score, double Threshold, bool Matched);
}
