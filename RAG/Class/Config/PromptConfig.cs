using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Ràng buộc độ dài câu trả lời, nối thêm vào system prompt của mọi nhánh trả lời.
    /// <para>
    /// Tồn tại vì <c>max_tokens</c> gửi lên API là một nhát cắt cứng: model cứ viết dài rồi bị
    /// chặt ngang, để lại câu cụt giữa chừng. Nói trước ngân sách cho model để nó tự tóm gọn,
    /// còn <c>max_tokens</c> lùi về đúng vai trò lưới an toàn.
    /// </para>
    /// </summary>
    public class AnswerLengthConfig
    {
        public bool Enabled { get; set; } = true;

        /// <summary>{0} = ngân sách token, {1} = số từ ước lượng.</summary>
        public string InstructionTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Chỉ thị nhắm dưới trần thật, vì bảo model viết đúng bằng trần mà API cũng cắt ở
        /// đúng chỗ đó thì câu cuối vẫn cụt.
        /// </summary>
        [Range(0.1, 1.0)]
        public double SafetyRatio { get; set; } = 0.85;

        /// <summary>
        /// Model đếm từ đáng tin hơn đếm token nhiều, nên template có sẵn cả con số từ để bám vào.
        /// </summary>
        [Range(0.1, 5.0)]
        public double WordsPerToken { get; set; } = 0.6;
    }

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

        /// <summary>Ràng buộc độ dài, dùng chung cho cả nhánh truy hồi lẫn nhánh trả lời thẳng.</summary>
        public AnswerLengthConfig AnswerLength { get; set; } = new();

        /// <summary>
        /// Dựng câu chỉ thị rút gọn từ trần token của provider đang dùng. Trả về chuỗi rỗng khi
        /// tắt hoặc chưa khai template, nên prompt giữ nguyên y như trước khi có tính năng này.
        /// </summary>
        public string BuildLengthInstruction(int maxOutputTokens)
        {
            if (!AnswerLength.Enabled || string.IsNullOrWhiteSpace(AnswerLength.InstructionTemplate))
                return string.Empty;

            var budget = Math.Max(1, (int)(maxOutputTokens * AnswerLength.SafetyRatio));
            var words = Math.Max(1, (int)(budget * AnswerLength.WordsPerToken));

            return string.Format(AnswerLength.InstructionTemplate, budget, words);
        }

        /// <param name="lengthInstruction">
        /// Chỉ thị độ dài đã render sẵn (xem <see cref="BuildLengthInstruction"/>), nối vào cuối
        /// system prompt. Nhận chuỗi đã render chứ không nhận con số token, để nơi duy nhất biết
        /// cách diễn đạt ngân sách vẫn là config này.
        /// </param>
        public string BuildSystemPrompt(string npcName, string npcPersonality, string lengthInstruction) =>
            string.Format(AnswerSystemTemplate, npcName, npcPersonality) + lengthInstruction;

        public string BuildUserPrompt(string context, string question) =>
            string.Format(AnswerUserTemplate, context, question);
    }
}
