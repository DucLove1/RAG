using RAG.Interface;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Decorator bọc quanh bộ chuẩn hóa thật để khỏi gọi LLM lại cho câu đã gặp.
    /// <para>
    /// Đây là chỗ tiết kiệm thời gian lớn nhất trên mỗi request trúng cache: chuẩn hóa là một lần
    /// gọi LLM đầy đủ (~300-800ms), đắt hơn hẳn một lần gọi embedding.
    /// </para>
    /// </summary>
    public sealed class CachingQueryNormalizer : IQueryNormalizer
    {
        private readonly IQueryNormalizer _inner;
        private readonly INormalizationCache _cache;
        private readonly ILogger<CachingQueryNormalizer> _logger;

        public CachingQueryNormalizer(IQueryNormalizer inner,
                                      INormalizationCache cache,
                                      ILogger<CachingQueryNormalizer> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public async Task<string> NormalizeAsync(string question, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question))
                return await _inner.NormalizeAsync(question, cancellationToken);

            var key = question.Trim();

            if (_cache.TryGetNormalizedQuestion(key, out var cached))
            {
                _logger.LogDebug("Trúng cache chuẩn hóa: {Question}", key);
                return cached;
            }

            var normalized = await _inner.NormalizeAsync(question, cancellationToken);

            if (string.IsNullOrWhiteSpace(normalized))
                return normalized;

            // Kết quả giống hệt câu gốc có hai khả năng không phân biệt được từ bên ngoài:
            // câu vốn đã chuẩn (đúng), hoặc bộ chuẩn hóa vừa fail-open vì LLM lỗi (sai).
            // Đánh dấu để cache cho nhóm này thời hạn ngắn hơn, giới hạn thiệt hại của khả năng thứ hai.
            var unchanged = string.Equals(normalized.Trim(), key, StringComparison.Ordinal);

            _cache.SetNormalizedQuestion(key, normalized, unchanged);

            return normalized;
        }
    }
}
