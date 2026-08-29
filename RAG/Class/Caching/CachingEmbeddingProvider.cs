using RAG.Extension;
using RAG.Interface;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Decorator bọc quanh nhà cung cấp embedding thật.
    /// <para>
    /// Đánh thẳng vào nút thắt chính: hạn mức request embedding mỗi phút của gói free tier.
    /// Đường batch cũng đi qua cache — chỉ những câu chưa có mới được gửi đi, rồi ghép lại đúng
    /// thứ tự ban đầu.
    /// </para>
    /// </summary>
    public sealed class CachingEmbeddingProvider : IEmbeddingProvider
    {
        private readonly IEmbeddingProvider _inner;
        private readonly IQueryCache _cache;
        private readonly ILogger<CachingEmbeddingProvider> _logger;

        public CachingEmbeddingProvider(IEmbeddingProvider inner,
                                        IQueryCache cache,
                                        ILogger<CachingEmbeddingProvider> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public string ModelId => _inner.ModelId;

        public Task<int> GetDimsAsync() => _inner.GetDimsAsync();

        public async Task<float[]> GetEmbeddingsAsync(string input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                return await _inner.GetEmbeddingsAsync(input, cancellationToken);

            if (_cache.TryGetEmbedding(input, out var cached))
            {
                _logger.LogDebug("Trúng cache embedding: {Input}", input);
                return cached;
            }

            var vector = await _inner.GetEmbeddingsAsync(input, cancellationToken);

            await CacheIfUsableAsync(input, vector);

            return vector;
        }

        public async Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(IReadOnlyList<string> inputs,
                                                                          CancellationToken cancellationToken = default)
        {
            if (inputs.Count == 0)
                return Array.Empty<float[]>();

            var results = new float[inputs.Count][];
            var missingIndexes = new List<int>();

            for (var i = 0; i < inputs.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(inputs[i]) && _cache.TryGetEmbedding(inputs[i], out var cached))
                    results[i] = cached;
                else
                    missingIndexes.Add(i);
            }

            if (missingIndexes.Count == 0)
            {
                _logger.LogDebug("Toàn bộ {Count} câu đều trúng cache embedding.", inputs.Count);
                return results;
            }

            if (missingIndexes.Count < inputs.Count)
            {
                _logger.LogInformation("Cache embedding phủ {Hit}/{Total} câu, chỉ gọi API cho phần còn lại.",
                    inputs.Count - missingIndexes.Count, inputs.Count);
            }

            var missingInputs = missingIndexes.Select(index => inputs[index]).ToList();
            var embedded = await _inner.GetEmbeddingsBatchAsync(missingInputs, cancellationToken);

            // Ghép lại theo đúng chỉ số ban đầu; hợp đồng của interface là giữ nguyên thứ tự và số lượng.
            for (var i = 0; i < missingIndexes.Count; i++)
            {
                var vector = i < embedded.Count ? embedded[i] : Array.Empty<float>();
                results[missingIndexes[i]] = vector;

                await CacheIfUsableAsync(missingInputs[i], vector);
            }

            return results;
        }

        /// <summary>
        /// Chỉ cache vector hợp lệ. Nhà cung cấp trả mảng rỗng khi API lỗi thay vì ném exception,
        /// nên cache vô điều kiện sẽ đóng băng một lần lỗi tạm thời thành vĩnh viễn: câu đó sẽ
        /// không bao giờ khớp route nào nữa.
        /// <para>
        /// Kiểm tra này là BẮT BUỘC chứ không phải nên có. Trước đây khởi động lại còn giới hạn được
        /// thiệt hại, nhưng từ khi cache được ghi xuống đĩa thì vector rác sẽ sống qua mọi lần khởi
        /// động lại. Bộ kiểm tra tương ứng khi nạp từ file nằm ở MemoryQueryCache.ImportSnapshot.
        /// </para>
        /// </summary>
        private async Task CacheIfUsableAsync(string input, float[] vector)
        {
            var dims = await _inner.GetDimsAsync();

            if (vector.Length != dims || !VectorMath.HasMagnitude(vector))
            {
                _logger.LogDebug("Không cache vector không hợp lệ cho: {Input}", input);
                return;
            }

            _cache.SetEmbedding(input, vector);
        }
    }
}
