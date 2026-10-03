using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Dto;
using RAG.Interface;

namespace RAG.Class.Conversation
{
    /// <summary>
    /// Gộp lịch sử hội thoại thành một chuỗi tóm tắt bằng một lượt gọi LLM.
    /// <para>
    /// KHÔNG fail-open như bộ chuẩn hóa hay router: ở đó lỗi còn đường lùi (dùng câu gốc, đi RAG),
    /// còn ở đây client đang chờ đúng một bản tóm tắt — trả về chuỗi rỗng hay bản ghi gốc thì client
    /// sẽ lặng lẽ thay lịch sử thật bằng thứ vô nghĩa. Lỗi phải lộ ra để client giữ lịch sử cũ và
    /// thử lại sau; bộ dịch lỗi có sẵn biến hết key thành 429.
    /// </para>
    /// </summary>
    public sealed class LlmConversationCompactor : IConversationCompactor
    {
        private readonly ILLMProvider _llmProvider;
        private readonly LlmRequestOptions _requestOptions;
        private readonly ConversationConfig _config;
        private readonly IConversationFormatter _formatter;
        private readonly ILogger<LlmConversationCompactor> _logger;

        public LlmConversationCompactor(ILlmProviderResolver llmProviderResolver,
                                        IOptions<ConversationConfig> options,
                                        IConversationFormatter formatter,
                                        ILogger<LlmConversationCompactor> logger)
        {
            _config = options.Value;
            _llmProvider = llmProviderResolver.Resolve(_config.Compaction.Provider);
            _requestOptions = new LlmRequestOptions(
                _config.Compaction.Model, _config.Compaction.ThinkingLevel, _config.Compaction.MaxOutputTokens);
            _formatter = formatter;
            _logger = logger;
        }

        public async Task<string> CompactAsync(ConversationHistory history, CancellationToken cancellationToken = default)
        {
            if (history.Turns.Count > _config.Compaction.MaxInputTurns)
            {
                throw new ConversationTooLongException(
                    $"Đoạn lịch sử có {history.Turns.Count} lượt, vượt giới hạn {_config.Compaction.MaxInputTurns} lượt.");
            }

            // Cắt độ dài từng lượt nhưng KHÔNG cắt số lượt theo MaxTurns: đó là giới hạn của prompt
            // trả lời, còn đây chính là chỗ gộp những lượt cũ mà prompt trả lời sẽ bỏ đi.
            var trimmed = _formatter.TrimLengths(history);

            var previousSummary = string.IsNullOrWhiteSpace(trimmed.Summary)
                ? _config.Compaction.NoPreviousSummaryText
                : trimmed.Summary;

            var summary = await _llmProvider.AskAsync(
                string.Format(_config.Compaction.SystemPromptTemplate, history.NpcName, _config.MaxSummaryLength),
                string.Format(_config.Compaction.UserPromptTemplate,
                    previousSummary,
                    _formatter.RenderTranscript(trimmed with { Summary = null })),
                _requestOptions,
                cancellationToken);

            // 200 mà không có chữ nào (thường là token suy nghĩ ăn hết trần) vẫn là hỏng: trả chuỗi
            // rỗng thì client sẽ thay lịch sử thật bằng một tóm tắt trống.
            if (string.IsNullOrWhiteSpace(summary))
                throw new InvalidOperationException("LLM trả về tóm tắt rỗng.");

            var result = _formatter.TrimSummary(summary);

            _logger.LogDebug("Đã gộp {Turns} lượt hội thoại với {Npc} thành tóm tắt {Length} ký tự.",
                history.Turns.Count, history.NpcName, result.Length);

            return result;
        }
    }
}
