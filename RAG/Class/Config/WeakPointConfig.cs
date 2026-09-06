using System.ComponentModel.DataAnnotations;
using RAG.Class.Constants;
using RAG.Class.Routing;

namespace RAG.Class.Config
{
    /// <summary>
    /// Một NPC có điểm yếu, kèm các câu chốt bắt bài được nhân vật đó.
    /// </summary>
    public class WeakPointTargetConfig
    {
        public string NpcName { get; set; } = string.Empty;

        /// <summary>
        /// Câu chốt viết ở dạng ĐÃ CHUẨN HÓA (đủ dấu, không viết tắt) vì node nhận câu hỏi đã qua
        /// bước chuẩn hóa. Đây là các câu MẪU đưa cho LLM tự so ngữ nghĩa, không phải chuỗi đem đi
        /// so bằng: người chơi diễn đạt kiểu khác hẳn vẫn phải trúng.
        /// </summary>
        public List<string> Triggers { get; set; } = new();

        /// <summary>
        /// Câu NPC nói khi bị bắt bài. CỐ TÌNH không qua LLM: đây là một nhịp kịch bản, để LLM diễn
        /// đạt lại thì mỗi lần chơi ra một kiểu và mất luôn tính xác định của nhịp đó. Để trống thì
        /// <c>answer</c> trả về chuỗi rỗng và client tự lo lời thoại.
        /// </summary>
        public string Reply { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cấu hình node phát hiện "trúng điểm yếu": đưa câu hỏi của người chơi lên LLM cùng danh sách
    /// câu chốt của NPC đang bị thẩm vấn, rồi hỏi LLM xem câu đó có trùng ý với câu chốt nào không.
    /// <para>
    /// Việc đánh giá độ tương đồng hoàn toàn do LLM làm. Không có phép so chuỗi nào tham gia vào
    /// quyết định — phần so chuỗi duy nhất trong node là bước đọc lại cái nhãn mà LLM vừa xuất ra.
    /// </para>
    /// </summary>
    public class WeakPointConfig : IValidatableObject
    {
        public const string SectionName = "WeakPoint";

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Provider dùng để đối chiếu, độc lập với provider trả lời — giống router và node chuẩn
        /// hóa, để bước phân loại không đốt hạn mức của pool sinh câu trả lời.
        /// </summary>
        public LlmProviderKey Provider { get; set; } = LlmProviderKey.Gemini;

        /// <summary>Để trống thì dùng model mặc định của provider.</summary>
        public string? Model { get; set; }

        /// <summary>
        /// Cổng độ dài. Rộng hơn router (200) một cách CỐ Ý và vì lý do ngược lại: ở router, cổng
        /// này để khỏi trả tiền phân loại cả đoạn văn; ở đây nó lặng lẽ nuốt mất một nhịp game khi
        /// người chơi gõ một lời buộc tội dài. Node ghi LogDebug mỗi lần cổng chặn để còn lần ra.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int MaxInputLength { get; set; } = 300;

        /// <summary>Nhãn LLM xuất khi câu hỏi trúng điểm yếu.</summary>
        public string HitLabel { get; set; } = "trung_diem_yeu";

        /// <summary>Nhãn LLM xuất khi không trúng.</summary>
        public string NoHitLabel { get; set; } = "khong_trung";

        /// <summary>{0} = khối câu chốt, {1} = nhãn trúng, {2} = nhãn không trúng, {3} = tên NPC.</summary>
        public string SystemPromptTemplate { get; set; } = string.Empty;

        /// <summary>{0} = câu hỏi của người chơi đã chuẩn hóa.</summary>
        public string UserPromptTemplate { get; set; } = string.Empty;

        /// <summary>Một dòng trong danh sách câu chốt; {0} = nội dung câu.</summary>
        public string TriggerBlockTemplate { get; set; } = "- {0}";

        public string TriggerSeparator { get; set; } = "\n";

        public List<WeakPointTargetConfig> Targets { get; set; } = new();

        public string BuildSystemPrompt(string triggerBlock, string npcName) =>
            string.Format(SystemPromptTemplate, triggerBlock, HitLabel, NoHitLabel, npcName);

        public string BuildUserPrompt(string question) =>
            string.Format(UserPromptTemplate, question);

        /// <summary>
        /// Chỉ chặn khởi động với những thứ KHÔNG thể thoái hóa an toàn — cùng triết lý với
        /// <see cref="SemanticRouterConfig"/>. Target hỏng (thiếu tên, không có câu chốt, thiếu lời
        /// thoại) chỉ bị bỏ qua kèm cảnh báo trong constructor của node.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enabled)
                yield break;

            if (string.IsNullOrWhiteSpace(HitLabel) || string.IsNullOrWhiteSpace(NoHitLabel))
            {
                yield return new ValidationResult(
                    "WeakPoint:HitLabel và WeakPoint:NoHitLabel đều không được để trống: thiếu một " +
                    "trong hai thì LLM không có đủ hai lựa chọn để trả lời.",
                    new[] { nameof(HitLabel), nameof(NoHitLabel) });

                yield break;
            }

            // So bằng Normalize chứ không phải OrdinalIgnoreCase: Normalize mới là hàm khóa mà
            // RouteLabelParser thực sự dùng để tra nhãn, nên hai nhãn "khác nhau" theo phép so
            // thông thường vẫn có thể trùng nhau dưới mắt bộ phân tích.
            var hit = RouteLabelParser.Normalize(HitLabel);
            var noHit = RouteLabelParser.Normalize(NoHitLabel);

            if (hit == noHit)
            {
                yield return new ValidationResult(
                    $"WeakPoint:HitLabel (\"{HitLabel}\") và NoHitLabel (\"{NoHitLabel}\") quy về cùng " +
                    $"một nhãn (\"{hit}\") sau chuẩn hóa, nên không phân biệt được trúng với không trúng.",
                    new[] { nameof(HitLabel), nameof(NoHitLabel) });

                yield break;
            }

            // Hai luật dưới đây là lớp chặn ĐỌC NHẦM, không phải chuyện thẩm mỹ đặt tên.
            // RouteLabelParser.ScanWholeOutput dò nhãn bằng CHUỖI CON và bằng tập từ, còn lưới an
            // toàn "thấy hai nhãn trở lên thì bỏ" của nó không thể kích hoạt khi tập nhãn chỉ có
            // một phần tử như ở đây. Đặt HitLabel = "trung_diem_yeu" và NoHitLabel =
            // "khong_trung_diem_yeu" thì một câu trả lời "khong_trung_diem_yeu" bị đọc thành TRÚNG,
            // và game lộ thủ phạm cho người chơi chưa suy luận ra. Node đã tắt ScanWholeOutput;
            // đây là lớp thứ hai, giữ lại phòng khi ai đó bật lại.
            if (hit.Contains(noHit, StringComparison.Ordinal) || noHit.Contains(hit, StringComparison.Ordinal))
            {
                yield return new ValidationResult(
                    $"WeakPoint:HitLabel (\"{hit}\") và NoHitLabel (\"{noHit}\") chứa nhau sau chuẩn hóa. " +
                    "Nhãn này là chuỗi con của nhãn kia thì bước đọc nhãn có thể đọc \"không trúng\" " +
                    "thành \"trúng\". Đặt hai nhãn không chứa nhau.",
                    new[] { nameof(HitLabel), nameof(NoHitLabel) });
            }

            var hitTokens = Tokenize(hit);
            var noHitTokens = Tokenize(noHit);

            if (hitTokens.IsSubsetOf(noHitTokens) || noHitTokens.IsSubsetOf(hitTokens))
            {
                yield return new ValidationResult(
                    $"WeakPoint:HitLabel (\"{hit}\") và NoHitLabel (\"{noHit}\") có tập từ lồng nhau. " +
                    "Bước đọc nhãn khớp cả theo tập từ, nên nhãn này sẽ nuốt nhãn kia.",
                    new[] { nameof(HitLabel), nameof(NoHitLabel) });
            }

            if (string.IsNullOrWhiteSpace(SystemPromptTemplate))
            {
                yield return new ValidationResult(
                    "WeakPoint:SystemPromptTemplate không được để trống khi Enabled = true.",
                    new[] { nameof(SystemPromptTemplate) });
            }
            else if (!SystemPromptTemplate.Contains("{0}", StringComparison.Ordinal) ||
                     !SystemPromptTemplate.Contains("{1}", StringComparison.Ordinal) ||
                     !SystemPromptTemplate.Contains("{2}", StringComparison.Ordinal))
            {
                yield return new ValidationResult(
                    "WeakPoint:SystemPromptTemplate phải chứa {0} (khối câu chốt), {1} (nhãn trúng) " +
                    "và {2} (nhãn không trúng); thiếu một cái thì LLM không nhìn thấy đủ đề bài.",
                    new[] { nameof(SystemPromptTemplate) });
            }

            if (string.IsNullOrWhiteSpace(UserPromptTemplate) ||
                !UserPromptTemplate.Contains("{0}", StringComparison.Ordinal))
            {
                yield return new ValidationResult(
                    "WeakPoint:UserPromptTemplate phải chứa {0} (câu hỏi của người chơi).",
                    new[] { nameof(UserPromptTemplate) });
            }

            // Trùng tên NPC là lỗi câm giống hệt trùng tên route: từ điển của node âm thầm nuốt mất
            // một mục, và không có gì báo rằng danh sách câu chốt thứ hai không bao giờ được dùng.
            var duplicates = Targets
                .Select(target => target.NpcName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                yield return new ValidationResult(
                    $"WeakPoint:Targets có NpcName trùng (không phân biệt hoa thường): {string.Join(", ", duplicates)}.",
                    new[] { nameof(Targets) });
            }
        }

        private static HashSet<string> Tokenize(string normalizedLabel) =>
            normalizedLabel
                .Split(RouteLabelSyntax.WordSeparator, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
    }
}
