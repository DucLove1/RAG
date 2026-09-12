using System.Globalization;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo kho vector.
    /// <para>
    /// Nhãn <c>hits</c> đi kèm là có chủ ý: một lần truy hồi 15ms trả về 0 kết quả và một lần 15ms
    /// trả về 5 kết quả là hai tình huống hoàn toàn khác nhau, mà chỉ nhìn thời gian thì không phân
    /// biệt được. Truy hồi 0 kết quả chính là nguyên nhân số một của câu trả lời "tôi không biết".
    /// </para>
    /// </summary>
    public sealed class TimedVectorStore : IVectorStore
    {
        private readonly IVectorStore _inner;
        private readonly ILatencyTracker _latency;

        public TimedVectorStore(IVectorStore inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public Task CreateCollectionAsync(ulong dimension, CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(
                LatencyStages.CollectionCreate, () => _inner.CreateCollectionAsync(dimension, cancellationToken));

        public Task EnsureCollectionExistsAsync(ulong dimension, CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(
                LatencyStages.CollectionEnsure, () => _inner.EnsureCollectionExistsAsync(dimension, cancellationToken));

        public Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(LatencyStages.VectorUpsert, () => _inner.UpsertAsync(records, cancellationToken));

        public async Task<IReadOnlyList<VectorHit>> SearchAsync(float[] queryVector,
                                                                 VectorSearchFilter filter,
                                                                 int topK,
                                                                 CancellationToken cancellationToken = default)
        {
            var hits = await _latency.TrackAsync(
                LatencyStages.VectorSearch, () => _inner.SearchAsync(queryVector, filter, topK, cancellationToken));

            _latency.Tag(LatencyTags.Hits, hits.Count.ToString(CultureInfo.InvariantCulture));

            return hits;
        }
    }
}
