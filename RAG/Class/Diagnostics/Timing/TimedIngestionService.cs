using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Gốc của phiên đo cho đường nạp dữ liệu.
    /// <para>
    /// Có mặt vì đường nạp dùng CHUNG <c>IEmbeddingProvider</c> và <c>IVectorStore</c> với đường trả
    /// lời. Không mở phiên ở đây thì mọi số đo của một lần nạp rơi vào hư không — mà nạp mới đúng là
    /// thao tác kéo dài hàng phút và đáng đo nhất.
    /// </para>
    /// </summary>
    public sealed class TimedIngestionService : IIngestionService
    {
        private readonly IIngestionService _inner;
        private readonly ILatencySessionFactory _sessions;

        public TimedIngestionService(IIngestionService inner, ILatencySessionFactory sessions)
        {
            _inner = inner;
            _sessions = sessions;
        }

        public async Task CreateCollectionAsync(CancellationToken cancellationToken = default)
        {
            using var session = _sessions.Begin(LatencyOperations.Ingest);

            await _inner.CreateCollectionAsync(cancellationToken);
        }

        public async Task<IngestionResult> IngestAsync(IReadOnlyList<DocumentSource> documents,
                                                       string npcNames,
                                                       CancellationToken cancellationToken = default)
        {
            using var session = _sessions.Begin(LatencyOperations.Ingest);

            return await _inner.IngestAsync(documents, npcNames, cancellationToken);
        }
    }
}
