using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Template prompt của bước sinh câu trả lời. Đưa ra ngoài configuration để
    /// <c>AskPipeline</c> không phải sửa code mỗi khi tinh chỉnh prompt (OCP).
    /// </summary>
    public class PromptConfig
    {
        public const string SectionName = "Prompts";

        /// <summary>{0} = tên NPC, {1} = tính cách NPC.</summary>
        [Required(AllowEmptyStrings = false)]
        public string AnswerSystemTemplate { get; set; } = string.Empty;

        /// <summary>{0} = ngữ cảnh truy hồi, {1} = câu hỏi đã chuẩn hóa.</summary>
        [Required(AllowEmptyStrings = false)]
        public string AnswerUserTemplate { get; set; } = string.Empty;

        /// <summary>Ký tự nối giữa các đoạn ngữ cảnh.</summary>
        public string ContextSeparator { get; set; } = "\n";

        public string BuildSystemPrompt(string npcName, string npcPersonality) =>
            string.Format(AnswerSystemTemplate, npcName, npcPersonality);

        public string BuildUserPrompt(string context, string question) =>
            string.Format(AnswerUserTemplate, context, question);
    }
}
