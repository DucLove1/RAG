using RAG.Class.Constants;
using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Lịch sử hội thoại do client gửi kèm câu hỏi, và endpoint gộp lịch sử thành tóm tắt.
    /// <para>
    /// Server không lưu phiên: client giữ lịch sử, gửi kèm mỗi lần hỏi, và tự quyết định khi nào
    /// gọi compact. Các giới hạn ở đây chỉ để prompt không vỡ khi client quên compact.
    /// </para>
    /// </summary>
    public class ConversationConfig : IValidatableObject
    {
        public const string SectionName = "Conversation";

        /// <summary>
        /// Số lượt CUỐI được giữ khi trả lời; lượt cũ hơn bị bỏ chứ không trả lỗi — client quên
        /// compact thì NPC chỉ quên bớt chuyện cũ, không làm hỏng request.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int MaxTurns { get; set; } = 12;

        /// <summary>Một lượt dài hơn ngưỡng này bị cắt bớt phần đuôi.</summary>
        [Range(1, int.MaxValue)]
        public int MaxTurnLength { get; set; } = 500;

        /// <summary>Tóm tắt dài hơn ngưỡng này bị cắt bớt — áp cho cả tóm tắt client gửi lên lẫn tóm tắt endpoint compact sinh ra.</summary>
        [Range(1, int.MaxValue)]
        public int MaxSummaryLength { get; set; } = 1500;

        /// <summary>Cách dựng lịch sử thành văn bản trong prompt.</summary>
        public ConversationRenderConfig Render { get; set; } = new();

        /// <summary>Endpoint gộp lịch sử thành một chuỗi tóm tắt.</summary>
        public ConversationCompactionConfig Compaction { get; set; } = new();

        /// <summary>
        /// [Range] trên lớp lồng không được ValidateDataAnnotations duyệt tới, nên phần đó kiểm tay ở đây.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var (name, template, placeholders) in new[]
            {
                (nameof(Render.SummaryTemplate), Render.SummaryTemplate, 1),
                (nameof(Render.PlayerLineTemplate), Render.PlayerLineTemplate, 1),
                (nameof(Render.NpcLineTemplate), Render.NpcLineTemplate, 2),
                (nameof(Render.AnswerBlockTemplate), Render.AnswerBlockTemplate, 2),
                (nameof(Compaction.SystemPromptTemplate), Compaction.SystemPromptTemplate, 2),
                (nameof(Compaction.UserPromptTemplate), Compaction.UserPromptTemplate, 2)
            })
            {
                if (!HasPlaceholders(template, placeholders))
                {
                    yield return new ValidationResult(
                        $"Conversation:{name} không được để trống và phải chứa {Placeholders(placeholders)}.",
                        new[] { name });
                }
            }

            if (Compaction.MaxInputTurns < 1)
            {
                yield return new ValidationResult(
                    "Conversation:Compaction:MaxInputTurns phải >= 1.",
                    new[] { nameof(Compaction.MaxInputTurns) });
            }

            if (Compaction.MaxOutputTokens < 1)
            {
                yield return new ValidationResult(
                    "Conversation:Compaction:MaxOutputTokens phải >= 1.",
                    new[] { nameof(Compaction.MaxOutputTokens) });
            }
        }

        private static bool HasPlaceholders(string template, int count) =>
            !string.IsNullOrWhiteSpace(template) &&
            Enumerable.Range(0, count).All(i => template.Contains("{" + i + "}", StringComparison.Ordinal));

        private static string Placeholders(int count) =>
            string.Join(", ", Enumerable.Range(0, count).Select(i => "{" + i + "}"));
    }

    /// <summary>
    /// Template dựng lịch sử thành văn bản. Dùng chung cho bộ chuẩn hóa, prompt trả lời và endpoint
    /// compact, để ba nơi nhìn lịch sử theo cùng một cách.
    /// Dấu ngoặc nhọn literal phải escape thành {{ }} vì mọi template đi qua string.Format.
    /// </summary>
    public class ConversationRenderConfig
    {
        /// <summary>Dòng tóm tắt đứng đầu bản ghi; {0} = tóm tắt.</summary>
        public string SummaryTemplate { get; set; } = string.Empty;

        /// <summary>Một lượt của người chơi; {0} = nội dung.</summary>
        public string PlayerLineTemplate { get; set; } = string.Empty;

        /// <summary>Một lượt của NPC; {0} = tên NPC, {1} = nội dung.</summary>
        public string NpcLineTemplate { get; set; } = string.Empty;

        /// <summary>Ký tự nối giữa các dòng của bản ghi.</summary>
        public string LineSeparator { get; set; } = "\n";

        /// <summary>
        /// Khối lịch sử gắn lên ĐẦU user prompt trả lời; {0} = tên NPC, {1} = bản ghi.
        /// Nên tự kết thúc bằng dòng trống để tách khỏi phần prompt phía sau, và nên nói rõ lịch sử
        /// chỉ để nói tiếp mạch chuyện — dữ kiện vẫn chỉ lấy từ ký ức.
        /// </summary>
        public string AnswerBlockTemplate { get; set; } = string.Empty;
    }

    /// <summary>Endpoint gộp các lượt hội thoại (kèm tóm tắt cũ nếu có) thành một chuỗi tóm tắt.</summary>
    public class ConversationCompactionConfig
    {
        /// <summary>Provider dùng để tóm tắt, độc lập với provider trả lời.</summary>
        public LlmProviderKey Provider { get; set; } = LlmProviderKey.Groq;

        /// <summary>Để trống thì dùng model mặc định của provider.</summary>
        public string? Model { get; set; }

        /// <summary>Để trống thì không gửi, model chạy theo mức suy nghĩ mặc định của nhà cung cấp.</summary>
        public LlmThinkingLevel? ThinkingLevel { get; set; }

        /// <summary>
        /// Trần token đầu ra riêng của lượt tóm tắt. Trần chung của provider được chỉnh cho câu thoại
        /// ngắn, và token suy nghĩ cũng tính vào trần này — dùng chung thì bản tóm tắt dễ bị cắt cụt.
        /// </summary>
        public int MaxOutputTokens { get; set; } = 1200;

        /// <summary>Số lượt tối đa một lần compact nhận; vượt thì trả 400.</summary>
        public int MaxInputTurns { get; set; } = 200;

        /// <summary>{0} = tên NPC, {1} = độ dài tối đa của tóm tắt (ký tự).</summary>
        public string SystemPromptTemplate { get; set; } = string.Empty;

        /// <summary>
        /// {0} = tóm tắt cũ (hoặc <see cref="NoPreviousSummaryText"/>), {1} = bản ghi các lượt mới.
        /// Tóm tắt cũ là một khối RIÊNG chứ không phải dòng đầu bản ghi: dựng chung thì mô hình coi
        /// nó như ghi chú phụ và bỏ mất nội dung cũ khi gộp.
        /// </summary>
        public string UserPromptTemplate { get; set; } = string.Empty;

        /// <summary>Điền vào chỗ tóm tắt cũ khi đây là lần compact đầu tiên.</summary>
        public string NoPreviousSummaryText { get; set; } = string.Empty;
    }
}
