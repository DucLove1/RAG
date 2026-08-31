using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Nạp tài liệu vào kho vector: rút văn bản → cắt đoạn → nhúng → ghi.
    /// <para>
    /// Toàn bộ chuỗi này trước đây nằm rải giữa controller (đọc file, chọn định dạng, cắt đoạn) và
    /// pipeline (nhúng, ghi). Gom về một chỗ thì controller quay lại đúng việc của nó là nhận
    /// request và trả response.
    /// </para>
    /// </summary>
    public sealed class DocumentIngestionService : IIngestionService
    {
        private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;
        private readonly IChunkingStrategy _chunker;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IVectorStore _vectorStore;
        private readonly ILogger<DocumentIngestionService> _logger;

        public DocumentIngestionService(IEnumerable<IDocumentTextExtractor> extractors,
                                        IChunkingStrategy chunker,
                                        IEmbeddingProvider embeddingProvider,
                                        IVectorStore vectorStore,
                                        ILogger<DocumentIngestionService> logger)
        {
            _extractors = extractors.ToList();
            _chunker = chunker;
            _embeddingProvider = embeddingProvider;
            _vectorStore = vectorStore;
            _logger = logger;
        }

        public Task CreateCollectionAsync(CancellationToken cancellationToken = default) =>
            _vectorStore.CreateCollectionAsync((ulong)_embeddingProvider.Dimensions, cancellationToken);

        public async Task<IngestionResult> IngestAsync(IReadOnlyList<DocumentSource> documents,
                                                       string npcNames,
                                                       CancellationToken cancellationToken = default)
        {
            var chunks = new List<DocumentChunk>();
            var processed = 0;
            var skipped = 0;

            foreach (var document in documents)
            {
                var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
                var extractor = _extractors.FirstOrDefault(candidate => candidate.Supports(extension));

                if (extractor is null)
                {
                    _logger.LogWarning("Bỏ qua {File}: chưa có bộ đọc nào nhận phần mở rộng {Extension}.",
                        document.FileName, extension);
                    skipped++;
                    continue;
                }

                var raw = await extractor.ExtractAsync(document.Content, cancellationToken);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    _logger.LogWarning("Bỏ qua {File}: không rút được nội dung nào.", document.FileName);
                    skipped++;
                    continue;
                }

                var before = chunks.Count;

                foreach (var chunk in _chunker.Chunk(raw))
                    chunks.Add(new DocumentChunk(npcNames, chunk, document.FileName));

                _logger.LogInformation("Đã cắt {Count} đoạn từ {File}.", chunks.Count - before, document.FileName);
                processed++;
            }

            if (chunks.Count == 0)
                return new IngestionResult(processed, skipped, 0);

            await StoreAsync(chunks, cancellationToken);

            return new IngestionResult(processed, skipped, chunks.Count);
        }

        /// <summary>
        /// Nhúng theo lô rồi ghi một lần. Bản trước nhúng từng đoạn một trong vòng lặp, tức là
        /// bỏ qua hoàn toàn đường batch của nhà cung cấp lẫn cache đang bọc quanh nó.
        /// </summary>
        private async Task StoreAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken)
        {
            await _vectorStore.EnsureCollectionExistsAsync((ulong)_embeddingProvider.Dimensions, cancellationToken);

            var texts = chunks.Select(chunk => chunk.Text).ToList();
            var vectors = await _embeddingProvider.GetEmbeddingsBatchAsync(texts, cancellationToken);

            var records = new List<VectorRecord>(chunks.Count);

            for (var i = 0; i < chunks.Count && i < vectors.Count; i++)
            {
                // Nhà cung cấp giữ đúng thứ tự và số lượng, nhưng đoạn nào nhúng hỏng vẫn có thể là
                // mảng rỗng. Ghi vector rỗng vào kho nghĩa là đoạn đó vĩnh viễn không bao giờ khớp.
                if (vectors[i].Length != _embeddingProvider.Dimensions)
                {
                    _logger.LogWarning("Bỏ đoạn thứ {Index} của {File}: vector không hợp lệ.",
                        i, chunks[i].Source);
                    continue;
                }

                records.Add(new VectorRecord(
                    Guid.NewGuid(),
                    vectors[i],
                    new Dictionary<string, object>
                    {
                        { PayloadFields.NpcNames, chunks[i].NpcNames },
                        { PayloadFields.Text, chunks[i].Text },
                        { PayloadFields.Source, chunks[i].Source ?? string.Empty }
                    }));
            }

            await _vectorStore.UpsertAsync(records, cancellationToken);

            _logger.LogInformation("Đã ghi {Count}/{Total} đoạn vào kho vector.", records.Count, chunks.Count);
        }
    }
}
