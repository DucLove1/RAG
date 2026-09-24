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
    /// Khung trình bày khối đồ thị trong prompt.
    /// <para>
    /// CHỈ chứa khung — tiêu đề, template một dòng, ký tự nối. Nhãn của quan hệ và của
    /// <c>trang_thai</c> lấy từ <c>ontology.json</c>; đưa chúng vào đây là tạo nguồn sự thật thứ
    /// hai cho từ vựng của đồ thị.
    /// </para>
    /// </summary>
    public class GraphPromptConfig
    {
        /// <summary>Nối khối nguyên văn với khối đồ thị.</summary>
        public string BlockSeparator { get; set; } = "\n\n";

        /// <summary>
        /// Dòng mở đầu khối đồ thị. Nên dặn mô hình rằng nhãn trong ngoặc là mức độ tin cậy — đó là
        /// thứ ngăn nó thuật lại tin đồn như một kết luận.
        /// </summary>
        public string BlockHeader { get; set; } = string.Empty;

        /// <summary>{0} = nguồn, {1} = quan hệ (đã tính phủ định), {2} = đích, {3} = độ tin cậy.</summary>
        [Required(AllowEmptyStrings = false)]
        public string RelationLineTemplate { get; set; } = "- {0} {1} {2} ({3})";

        /// <summary>Nối tiêu đề với dòng đầu và giữa các dòng.</summary>
        public string RelationSeparator { get; set; } = "\n";
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

        /// <summary>Khung trình bày khối đồ thị. Xem <see cref="GraphPromptConfig"/>.</summary>
        public GraphPromptConfig Graph { get; set; } = new();

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

        /// <summary>
        /// Bản có khối đồ thị. Khối rỗng thì trả ĐÚNG chuỗi của bản hai tham số, từng byte — bật hay
        /// tắt <c>Graph:Enabled</c> không đổi prompt của câu hỏi không có cạnh nào.
        /// <para>
        /// Khối đồ thị đặt SAU nguyên văn: template để câu hỏi ở cuối, nên phần cuối của ngữ cảnh là
        /// phần nằm gần câu hỏi nhất.
        /// </para>
        /// </summary>
        public string BuildUserPrompt(string context, string graphBlock, string question)
        {
            if (string.IsNullOrEmpty(graphBlock))
                return BuildUserPrompt(context, question);

            var combined = string.IsNullOrEmpty(context) ? graphBlock : context + Graph.BlockSeparator + graphBlock;

            return BuildUserPrompt(combined, question);
        }
    }
}
