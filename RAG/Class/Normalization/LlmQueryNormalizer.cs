using Microsoft.Extensions.Options;
using RAG.Class.Dto;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Normalization
{
    /// <summary>
    /// Chuẩn hóa câu hỏi bằng LLM: mở rộng viết tắt, sửa chính tả, thêm dấu — và khi có lịch sử
    /// hội thoại thì viết lại câu nối tiếp thành câu độc lập.
    /// Thiết kế fail-open: mọi lỗi/kết quả bất thường đều rơi về câu hỏi gốc để không chặn pipeline.
    /// </summary>
    public class LlmQueryNormalizer : IQueryNormalizer
    {
        private readonly ILLMProvider _llmProvider;
        private readonly LlmRequestOptions _requestOptions;
        private readonly QueryNormalizationConfig _config;
        private readonly IConversationFormatter _conversationFormatter;
        private readonly ILogger<LlmQueryNormalizer> _logger;

        public LlmQueryNormalizer(
            ILlmProviderResolver llmProviderResolver,
            IOptions<QueryNormalizationConfig> options,
            IConversationFormatter conversationFormatter,
            ILogger<LlmQueryNormalizer> logger)
        {
            _config = options.Value;
            _llmProvider = llmProviderResolver.Resolve(_config.Provider);
            _requestOptions = new LlmRequestOptions(_config.Model, _config.ThinkingLevel);
            _conversationFormatter = conversationFormatter;
            _logger = logger;
        }

        public async Task<string> NormalizeAsync(string question,
                                                 ConversationHistory history,
                                                 CancellationToken cancellationToken = default)
        {
            if (!ShouldNormalize(question))
                return question;

            var withHistory = !history.IsEmpty && _config.CanRewriteWithHistory;

            try
            {
                var normalized = await _llmProvider.AskAsync(
                    withHistory ? _config.HistorySystemPrompt : _config.SystemPrompt,
                    withHistory ? BuildHistoryUserPrompt(history, question) : _config.BuildUserPrompt(question),
                    _requestOptions,
                    cancellationToken);

                if (!IsAcceptable(normalized, question, withHistory))
                {
                    _logger.LogDebug("Kết quả chuẩn hóa bị loại bỏ, dùng câu hỏi gốc: {Question}", question);
                    return question;
                }

                var result = normalized.Trim();
                _logger.LogDebug("Chuẩn hóa câu hỏi: {Original} -> {Normalized}", question, result);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chuẩn hóa câu hỏi thất bại, dùng câu hỏi gốc.");
                return question;
            }
        }

        private bool ShouldNormalize(string question) =>
            !string.IsNullOrWhiteSpace(question) && question.Length <= _config.MaxInputLength;

        private string BuildHistoryUserPrompt(ConversationHistory history, string question) =>
            _config.BuildHistoryUserPrompt(
                _conversationFormatter.RenderTranscript(history.TakeLast(_config.HistoryMaxTurns)),
                question);

        private bool IsAcceptable(string normalized, string original, bool withHistory)
        {
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            var length = normalized.Trim().Length;

            return withHistory
                ? length <= _config.HistoryMaxOutputLength
                : length <= original.Length * _config.MaxLengthRatio;
        }
    }
}
