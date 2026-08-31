using RAG.Interface;

namespace RAG.Class
{
    /// <summary>
    /// Cài đặt <see cref="IApiKeyRotator"/> với logic luân chuyển key và cooldown tự phục hồi.
    /// Key bị rate limit (429) sẽ được đánh dấu + timestamp, tự động quay lại sau cooldown.
    /// Thread-safe (provider là singleton, request song song).
    /// </summary>
    public sealed class ApiKeyRotator : IApiKeyRotator
    {
        private readonly IReadOnlyList<string> _keys;
        private readonly string _providerName;
        private readonly ILogger<ApiKeyRotator> _logger;
        private readonly TimeSpan _cooldown;

        private readonly DateTimeOffset?[] _rateLimitedAt;
        private int _currentIndex;
        private readonly object _lock = new();

        public ApiKeyRotator(IReadOnlyList<string> keys,
                            string providerName,
                            TimeSpan cooldown,
                            ILogger<ApiKeyRotator> logger)
        {
            if (keys.Count == 0)
                throw new ArgumentException("Pool API key không được trống.", nameof(keys));

            _keys = keys;
            _providerName = providerName;
            _logger = logger;
            _cooldown = cooldown;
            _rateLimitedAt = new DateTimeOffset?[keys.Count];
            _currentIndex = 0;
        }

        public string GetCurrentKey()
        {
            lock (_lock)
            {
                int attempts = 0;
                var now = DateTimeOffset.UtcNow;

                while (attempts < _keys.Count)
                {
                    // Key đủ điều kiện dùng nếu chưa bao giờ bị rate limit,
                    // hoặc đã qua thời gian cooldown.
                    if (_rateLimitedAt[_currentIndex] is null ||
                        now - _rateLimitedAt[_currentIndex] >= _cooldown)
                    {
                        return _keys[_currentIndex];
                    }

                    _currentIndex = (_currentIndex + 1) % _keys.Count;
                    attempts++;
                }

                throw new AllApiKeysRateLimitedException(
                    $"Mọi API key của {_providerName} ({_keys.Count} key) đều đang trong thời gian hồi phục từ rate limit. " +
                    $"Vui lòng thử lại sau {_cooldown.TotalSeconds} giây.");
            }
        }

        public void ReportRateLimited(string key)
        {
            lock (_lock)
            {
                int index = -1;
                for (int i = 0; i < _keys.Count; i++)
                {
                    if (_keys[i] == key)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                    return; // Key không nằm trong pool (không nên xảy ra)

                _rateLimitedAt[index] = DateTimeOffset.UtcNow;

                _logger.LogWarning(
                    "API key #{Index}/{Total} của {Provider} bị giới hạn tần suất (429). " +
                    "Sẽ dùng lại sau {Seconds} giây.",
                    index, _keys.Count, _providerName, _cooldown.TotalSeconds);

                _currentIndex = (_currentIndex + 1) % _keys.Count;
            }
        }
    }
}
