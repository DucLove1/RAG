using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo bước nhúng.
    /// <para>
    /// Đường đơn và đường batch được đo dưới HAI stage khác nhau: đường đơn là một request của người
    /// chơi, đường batch là cả một lần nạp tài liệu có thể kéo dài hàng phút vì <c>BatchDelaySeconds</c>.
    /// Gộp chung thì con số nhúng của đường trả lời sẽ bị đường nạp làm cho vô nghĩa.
    /// </para>
    /// </summary>
    public sealed class TimedEmbeddingProvider : IEmbeddingProvider
    {
        private readonly IEmbeddingProvider _inner;
        private readonly ILatencyTracker _latency;

        public TimedEmbeddingProvider(IEmbeddingProvider inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        // Không đo: đọc thuộc tính, không có I/O nào.
        public string ModelId => _inner.ModelId;

        public int Dimensions => _inner.Dimensions;

        public Task<float[]> GetEmbeddingsAsync(string input, CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(LatencyStages.Embedding, () => _inner.GetEmbeddingsAsync(input, cancellationToken));

        public Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(IReadOnlyList<string> inputs,
                                                                    CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(LatencyStages.EmbeddingBatch, () => _inner.GetEmbeddingsBatchAsync(inputs, cancellationToken));
    }
}
