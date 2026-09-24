using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo bước tra nguyên văn theo mã chunk. Trên đường trả lời nó nằm cuối nhánh đồ thị, nên
    /// <c>graphSearch + chunkResolve</c> mới là thời gian đầy đủ của nhánh đó. Endpoint tra chunk
    /// thủ công cũng đi qua đây, nhưng không mở phiên đo nên không ghi gì.
    /// </summary>
    public sealed class TimedChunkTextLookup : IChunkTextLookup
    {
        private readonly IChunkTextLookup _inner;
        private readonly ILatencyTracker _latency;

        public TimedChunkTextLookup(IChunkTextLookup inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public Task<IReadOnlyDictionary<string, ChunkText>> GetByCodesAsync(string npcName,
                                                                           IReadOnlyCollection<string> codes,
                                                                           CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(
                LatencyStages.ChunkResolve, () => _inner.GetByCodesAsync(npcName, codes, cancellationToken));
    }
}
