using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Normalization
{
    /// <summary>
    /// Chuẩn hóa câu hỏi bằng LLM: mở rộng viết tắt, sửa chính tả, thêm dấu.
    /// Thiết kế fail-open: mọi lỗi/kết quả bất thường đều rơi về câu hỏi gốc để không chặn pipeline.
    /// </summary>
    public class LlmQueryNormalizer : IQueryNormalizer
    {
        private readonly ILLMProvider _llmProvider;
        private readonly QueryNormalizationConfig _config;
        private readonly ILogger<LlmQueryNormalizer> _logger;

        public LlmQueryNormalizer(
            ILlmProviderResolver llmProviderResolver,
            IOptions<QueryNormalizationConfig> options,
            ILogger<LlmQueryNormalizer> logger)
        {
            _config = options.Value;
            _llmProvider = llmProviderResolver.Resolve(_config.Provider);
            _logger = logger;
        }

        public async Task<string> NormalizeAsync(string question, CancellationToken cancellationToken = default)
        {
            if (!ShouldNormalize(question))
                return question;

            try
            {
                var normalized = await _llmProvider.AskAsync(
                    _config.SystemPrompt,
                    _config.BuildUserPrompt(question),
                    _config.Model,
                    cancellationToken);

                if (!IsAcceptable(normalized, question))
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

        private bool IsAcceptable(string normalized, string original) =>
            !string.IsNullOrWhiteSpace(normalized) &&
            normalized.Trim().Length <= original.Length * _config.MaxLengthRatio;
    }
}
