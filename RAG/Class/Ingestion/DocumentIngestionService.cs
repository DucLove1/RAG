using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using System.Security.Cryptography;
using System.Text;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Nạp tài liệu vào kho vector: rút văn bản → suy phân quyền → cắt đoạn → nhúng → ghi.
    /// <para>
    /// Toàn bộ chuỗi này trước đây nằm rải giữa controller (đọc file, chọn định dạng, cắt đoạn) và
    /// pipeline (nhúng, ghi). Gom về một chỗ thì controller quay lại đúng việc của nó là nhận
    /// request và trả response.
    /// </para>
    /// <para>
    /// Đây cũng là nơi KHÓA GHÉP với đồ thị tri thức được sinh ra: số thứ tự đoạn cộng mã tài liệu
    /// thành <c>&lt;doc_id&gt;#L&lt;n&gt;</c>. Cả nhánh đồ thị phụ thuộc vào đúng một method ở đây,
    /// nên mọi thay đổi trong <see cref="BuildChunks"/> phải được kiểm lại bằng cách so một mã chunk
    /// với dòng tương ứng trong file nguồn.
    /// </para>
    /// </summary>
    public sealed class DocumentIngestionService : IIngestionService
    {
        private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;
        private readonly IChunkingStrategy _chunker;
        private readonly IAccessPolicy _accessPolicy;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IVectorStore _vectorStore;
        private readonly IngestionConfig _config;
        private readonly ILogger<DocumentIngestionService> _logger;

        public DocumentIngestionService(IEnumerable<IDocumentTextExtractor> extractors,
                                        IChunkingStrategy chunker,
                                        IAccessPolicy accessPolicy,
                                        IEmbeddingProvider embeddingProvider,
                                        IVectorStore vectorStore,
                                        IOptions<IngestionConfig> options,
                                        ILogger<DocumentIngestionService> logger)
        {
            _extractors = extractors.ToList();
            _chunker = chunker;
            _accessPolicy = accessPolicy;
            _embeddingProvider = embeddingProvider;
            _vectorStore = vectorStore;
            _config = options.Value;
            _logger = logger;
        }

        public Task CreateCollectionAsync(CancellationToken cancellationToken = default) =>
            _vectorStore.CreateCollectionAsync((ulong)_embeddingProvider.Dimensions, cancellationToken);

        public async Task<IngestionResult> IngestAsync(IReadOnlyList<DocumentSource> documents,
                                                       string npcNames,
                                                       CancellationToken cancellationToken = default)
        {
            var extracted = new List<ExtractedFile>();
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

                var content = await extractor.ExtractAsync(document.Content, cancellationToken);

                if (string.IsNullOrWhiteSpace(content.Body))
                {
                    _logger.LogWarning("Bỏ qua {File}: không rút được nội dung nào.", document.FileName);
                    skipped++;
                    continue;
                }

                extracted.Add(new ExtractedFile(document.FileName, content));
            }

            if (extracted.Count == 0)
                return new IngestionResult(0, skipped, 0);

            var chunks = BuildChunks(extracted, npcNames);

            if (chunks.Count == 0)
                return new IngestionResult(extracted.Count, skipped, 0);

            await StoreAsync(chunks, cancellationToken);

            return new IngestionResult(extracted.Count, skipped, chunks.Count);
        }

        /// <summary>
        /// Cắt đoạn rồi gắn mã chunk, phân quyền và siêu dữ liệu.
        /// <para>
        /// Phân quyền giải MỘT LẦN cho cả lô chứ không theo từng file, vì tài liệu bối cảnh thuộc về
        /// mọi NPC và danh sách "mọi NPC" chỉ dựng được sau khi đã đọc hết các file khác. Lô không
        /// có front matter (file .txt tải lên lẻ) rơi về <paramref name="fallbackNpcNames"/> mà
        /// người gọi gửi kèm — đường cũ vẫn chạy y nguyên.
        /// </para>
        /// </summary>
        private List<DocumentChunk> BuildChunks(IReadOnlyList<ExtractedFile> files, string fallbackNpcNames)
        {
            var access = _accessPolicy
                .Resolve(files.Select(file => file.Content.Metadata).ToList())
                .ToList();

            var chunks = new List<DocumentChunk>();

            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                var metadata = file.Content.Metadata;

                // doc_id trong front matter là NGUỒN SỰ THẬT — đồ thị trỏ về chính nó. Tên file chỉ
                // là phương án dự phòng cho tài liệu không có front matter.
                var docId = metadata.TryGetValue(FrontMatterFields.DocId, out var declared) && declared.Length > 0
                    ? declared
                    : Path.GetFileNameWithoutExtension(file.FileName);

                var npcNames = access[index].NpcNames.Count > 0
                    ? string.Join(FrontMatterFields.SourceSeparator, access[index].NpcNames)
                    : fallbackNpcNames;

                var before = chunks.Count;

                foreach (var segment in _chunker.Chunk(file.Content.Body))
                {
                    chunks.Add(new DocumentChunk(
                        npcNames,
                        segment.Text,
                        file.FileName,
                        segment.Ordinal is { } ordinal ? ChunkCodes.Build(docId, ordinal) : null,
                        metadata));
                }

                _logger.LogInformation("Đã cắt {Count} đoạn từ {File} ({DocId}) cho {Npc}.",
                    chunks.Count - before, file.FileName, docId, npcNames);
            }

            return chunks;
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

                records.Add(new VectorRecord(BuildId(chunks[i]), vectors[i], BuildPayload(chunks[i])));
            }

            await _vectorStore.UpsertAsync(records, cancellationToken);

            _logger.LogInformation("Đã ghi {Count}/{Total} đoạn vào kho vector.", records.Count, chunks.Count);
        }

        /// <summary>
        /// Id của điểm trong kho vector.
        /// <para>
        /// Dẫn xuất từ mã chunk khi có mã, để việc nạp trở thành GHI ĐÈ thay vì thêm mới: nạp lại
        /// cùng một corpus sau khi sửa một dòng không được đẻ ra bản sao của toàn bộ số điểm cũ.
        /// Tính chất đó là điều kiện để một bài đo lặp lại được.
        /// </para>
        /// <para>
        /// Hệ quả phải biết: nạp lại cùng một file dưới danh sách NPC khác giờ là GHI ĐÈ quyền cũ
        /// chứ không phải thêm một điểm thứ hai. Đó đúng là ngữ nghĩa mong muốn, nhưng nó là một
        /// thay đổi hành vi so với bản dùng <c>Guid.NewGuid()</c>.
        /// </para>
        /// </summary>
        private Guid BuildId(DocumentChunk chunk)
        {
            if (!_config.DeterministicChunkIds || chunk.ChunkCode is null)
                return Guid.NewGuid();

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(chunk.ChunkCode));

            return new Guid(hash.AsSpan(0, 16));
        }

        /// <summary>
        /// Chỉ ghi những trường CÓ giá trị. Đoạn cắt theo câu không có mã chunk, và payload của nó
        /// phải giữ nguyên hình dạng cũ — không được đẻ thêm một trường rỗng mà bộ lọc khớp nhầm.
        /// </summary>
        private static Dictionary<string, object> BuildPayload(DocumentChunk chunk)
        {
            var payload = new Dictionary<string, object>
            {
                { PayloadFields.NpcNames, chunk.NpcNames },
                { PayloadFields.Text, chunk.Text },
                { PayloadFields.Source, chunk.Source ?? string.Empty }
            };

            if (chunk.ChunkCode is not null)
            {
                payload[PayloadFields.ChunkCode] = chunk.ChunkCode;
                payload[PayloadFields.DocId] = ChunkCodes.DocId(chunk.ChunkCode);
            }

            Copy(chunk.Metadata, FrontMatterFields.Kind, payload, PayloadFields.Kind);
            Copy(chunk.Metadata, FrontMatterFields.Scene, payload, PayloadFields.Scene);
            Copy(chunk.Metadata, FrontMatterFields.Title, payload, PayloadFields.Title);

            return payload;
        }

        private static void Copy(IReadOnlyDictionary<string, string> metadata,
                                 string metadataKey,
                                 Dictionary<string, object> payload,
                                 string payloadKey)
        {
            if (metadata.TryGetValue(metadataKey, out var value) && value.Length > 0)
                payload[payloadKey] = value;
        }

        private sealed record ExtractedFile(string FileName, ExtractedDocument Content);
    }
}
