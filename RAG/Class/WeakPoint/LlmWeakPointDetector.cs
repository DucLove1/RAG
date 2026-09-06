using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Routing;
using RAG.Interface;

namespace RAG.Class.WeakPoint
{
    /// <summary>
    /// Đối chiếu câu hỏi của người chơi với danh sách câu chốt của NPC bằng MỘT lượt gọi LLM.
    /// <para>
    /// Việc so độ tương đồng là của LLM: câu chốt được nhúng vào system prompt làm câu mẫu, và mô
    /// hình tự phán đoán câu người chơi có nói đúng ý đó không. Nhờ vậy một cách diễn đạt khác hẳn
    /// về mặt chữ vẫn trúng. Trong lớp này không có phép so chuỗi nào tham gia vào quyết định —
    /// chỗ duy nhất đụng tới chuỗi là bước đọc lại cái nhãn mà LLM vừa xuất ra.
    /// </para>
    /// <para>
    /// Mọi thứ đắt tiền (prompt từng NPC, từ điển tra cứu, bộ đọc nhãn) dựng một lần trong
    /// constructor — hợp lệ vì cấu hình bind qua <c>IOptions</c> chứ không phải
    /// <c>IOptionsMonitor</c>, giống <see cref="LlmSemanticRouter"/>.
    /// </para>
    /// </summary>
    public sealed class LlmWeakPointDetector : IWeakPointDetector
    {
        private readonly ILLMProvider _llmProvider;
        private readonly WeakPointConfig _config;
        private readonly ILogger<LlmWeakPointDetector> _logger;
        private readonly RouteLabelParser _parser;

        /// <summary>Tên NPC → prompt và lời thoại dựng sẵn; mỗi request chỉ còn một phép tra.</summary>
        private readonly IReadOnlyDictionary<string, ResolvedWeakPoint> _targets;

        public LlmWeakPointDetector(ILlmProviderResolver llmProviderResolver,
                                    IOptions<WeakPointConfig> options,
                                    ILogger<LlmWeakPointDetector> logger)
        {
            _config = options.Value;
            _logger = logger;

            // Resolve theo khóa cấu hình riêng, độc lập với provider của đường trả lời — giống
            // router và node chuẩn hóa.
            _llmProvider = llmProviderResolver.Resolve(_config.Provider);

            // Tập nhãn chỉ có MỘT phần tử, nên bắt buộc tắt bước quét toàn đầu ra: bước đó dò bằng
            // chuỗi con và lưới an toàn "hai nhãn trở lên thì bỏ" của nó không thể kích hoạt với
            // một nhãn. Bật lên là mở đường cho việc đọc "không trúng" thành "trúng", tức lộ thủ
            // phạm. WeakPointConfig.Validate còn chặn thêm một lớp ở phía cấu hình nhãn.
            _parser = new RouteLabelParser(new[] { _config.HitLabel },
                                           _config.NoHitLabel,
                                           allowWholeOutputScan: false);

            _targets = BuildTargets();

            if (_targets.Count == 0)
                _logger.LogWarning("Không có NPC nào có điểm yếu dùng được; mọi câu hỏi sẽ đi đường thường.");
            else
                _logger.LogInformation("Đã nạp điểm yếu cho NPC: {Npcs}.", string.Join(", ", _targets.Keys));
        }

        public async Task<WeakPointMatch?> DetectAsync(string npcName,
                                                       string question,
                                                       CancellationToken cancellationToken = default)
        {
            // NPC không phải mục tiêu thì thoát TRƯỚC khi tốn bất kỳ lượt gọi nào. Đây là thứ giữ
            // chi phí của node ở mức "một lượt LLM cho MỘT NPC" chứ không phải cho mọi câu hỏi.
            if (string.IsNullOrWhiteSpace(npcName) || !_targets.TryGetValue(npcName, out var target))
                return null;

            if (!ShouldDetect(question))
            {
                // Cố tình ghi log: cổng này lặng lẽ nuốt mất một nhịp game khi người chơi gõ lời
                // buộc tội dài, và không có dòng này thì triệu chứng không thể lần ra được.
                _logger.LogDebug("Bỏ qua đối chiếu điểm yếu của {Npc}: câu rỗng hoặc dài quá {Max} ký tự.",
                    npcName, _config.MaxInputLength);

                return null;
            }

            try
            {
                var output = await _llmProvider.AskAsync(
                    target.SystemPrompt,
                    _config.BuildUserPrompt(question),
                    _config.Model,
                    cancellationToken);

                var resolution = _parser.Resolve(output);

                // Đây là công cụ tinh chỉnh prompt DUY NHẤT của node: khi trúng, pipeline thoát sớm
                // nên không còn dấu vết nào khác để đọc.
                _logger.LogDebug("Đối chiếu điểm yếu {Npc}: {Outcome} (đầu ra thô: \"{Output}\").",
                    npcName, resolution.Outcome, output);

                // Chỉ Matched mới là trúng. NoMatch và Unparseable đều về không trúng: báo trúng
                // nhầm là lộ thủ phạm, còn bỏ sót thì người chơi hỏi lại một câu là xong.
                // resolution.RouteName cố tình bỏ qua — tập nhãn chỉ có một phần tử nên không có
                // gì để tra ngược, đừng "hoàn thiện" chỗ này.
                return resolution.Outcome == RouteLabelOutcome.Matched ? target.Match : null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Đối chiếu điểm yếu của {Npc} thất bại, coi như KHÔNG trúng.", npcName);
                return null;
            }
        }

        private bool ShouldDetect(string question) =>
            !string.IsNullOrWhiteSpace(question) && question.Length <= _config.MaxInputLength;

        /// <summary>
        /// Bỏ target không dùng được kèm cảnh báo thay vì chặn khởi động — cùng chính sách với
        /// <see cref="RouteTableFactory"/>: một mục cấu hình hỏng không đáng để cả API không lên.
        /// </summary>
        private IReadOnlyDictionary<string, ResolvedWeakPoint> BuildTargets()
        {
            var resolved = new Dictionary<string, ResolvedWeakPoint>(StringComparer.OrdinalIgnoreCase);

            foreach (var target in _config.Targets)
            {
                if (string.IsNullOrWhiteSpace(target.NpcName))
                {
                    _logger.LogWarning("Bỏ qua một mục WeakPoint:Targets thiếu NpcName.");
                    continue;
                }

                var triggers = target.Triggers
                    .Where(trigger => !string.IsNullOrWhiteSpace(trigger))
                    .ToList();

                if (triggers.Count == 0)
                {
                    _logger.LogWarning("Bỏ qua điểm yếu của {Npc}: không có câu chốt nào.", target.NpcName);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(target.Reply))
                {
                    // Không chặn: cờ trả về cho client mới là mục đích chính, lời thoại có thể do
                    // client lo. Nhưng phải nói ra, vì một answer rỗng trông y hệt một lỗi.
                    _logger.LogWarning(
                        "Điểm yếu của {Npc} không có Reply; khi trúng, answer sẽ là chuỗi rỗng.",
                        target.NpcName);
                }

                var triggerBlock = string.Join(
                    _config.TriggerSeparator,
                    triggers.Select(trigger => string.Format(_config.TriggerBlockTemplate, trigger)));

                resolved[target.NpcName] = new ResolvedWeakPoint(
                    _config.BuildSystemPrompt(triggerBlock, target.NpcName),
                    new WeakPointMatch(target.Reply));
            }

            return resolved;
        }

        /// <summary>Một target đã giải xong template, sẵn sàng dùng lại cho mọi request.</summary>
        private sealed record ResolvedWeakPoint(string SystemPrompt, WeakPointMatch Match);
    }
}
