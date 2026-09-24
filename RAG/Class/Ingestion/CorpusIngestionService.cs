using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Extension;
using RAG.Interface;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Nạp toàn bộ thư mục corpus trong một lệnh, và soi bảng phân quyền suy ra từ front matter.
    /// <para>
    /// Đọc file theo thứ tự TÊN, cùng thứ tự với <c>sorted(glob(...))</c> trong công thức tham chiếu
    /// ở HUONG_DAN.md. Thứ tự không ảnh hưởng mã chunk (mã tính trong phạm vi từng tài liệu) nhưng nó
    /// quyết định thứ tự danh sách NPC của tài liệu bối cảnh — và danh sách đó đi thẳng vào payload,
    /// nên nó phải giống hệt nhau giữa hai lần nạp.
    /// </para>
    /// </summary>
    public sealed class CorpusIngestionService : ICorpusIngestionService
    {
        private readonly IIngestionService _ingestionService;
        private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;
        private readonly IChunkingStrategy _chunker;
        private readonly IAccessPolicy _accessPolicy;
        private readonly IngestionConfig _config;
        private readonly ILogger<CorpusIngestionService> _logger;

        public CorpusIngestionService(IIngestionService ingestionService,
                                      IEnumerable<IDocumentTextExtractor> extractors,
                                      IChunkingStrategy chunker,
                                      IAccessPolicy accessPolicy,
                                      IOptions<IngestionConfig> options,
                                      ILogger<CorpusIngestionService> logger)
        {
            _ingestionService = ingestionService;
            _extractors = extractors.ToList();
            _chunker = chunker;
            _accessPolicy = accessPolicy;
            _config = options.Value;
            _logger = logger;
        }

        public async Task<CorpusIngestionReport> IngestCorpusAsync(CancellationToken cancellationToken = default)
        {
            var files = CorpusFiles();

            var streams = new List<Stream>(files.Count);

            try
            {
                var documents = new List<DocumentSource>(files.Count);

                foreach (var path in files)
                {
                    var stream = File.OpenRead(path);
                    streams.Add(stream);
                    documents.Add(new DocumentSource(Path.GetFileName(path), stream));
                }

                // npcNames dự phòng để rỗng: mọi file corpus đều có front matter, nên phân quyền
                // luôn suy ra được. Truyền một cái tên vào đây sẽ biến một file thiếu front matter
                // từ lỗi thấy được thành một file âm thầm gán nhầm quyền.
                var result = await _ingestionService.IngestAsync(documents, string.Empty, cancellationToken);

                // Đọc lại front matter một lượt nữa để dựng bảng quyền. Với mười mấy file vài KB thì
                // đó là cái giá rẻ hơn nhiều so với việc mở rộng chữ ký của IIngestionService chỉ để
                // mang một bảng chẩn đoán đi ngược ra ngoài.
                var access = await DescribeAccessAsync(cancellationToken);

                _logger.LogInformation("Đã nạp corpus: {Files} file, {Chunks} đoạn, {Npc} NPC.",
                    result.FilesProcessed, result.ChunksIngested, access.SelectMany(doc => doc.NpcNames).Distinct().Count());

                return new CorpusIngestionReport(result, access);
            }
            finally
            {
                foreach (var stream in streams)
                    await stream.DisposeAsync();
            }
        }

        public async Task<IReadOnlyList<CorpusDocumentReport>> DescribeAccessAsync(
            CancellationToken cancellationToken = default)
        {
            var read = await ReadAllAsync(cancellationToken);

            return read
                .Select(entry => new CorpusDocumentReport(entry.DocId, entry.FileName, entry.NpcNames))
                .ToList();
        }

        public async Task<IReadOnlyList<CorpusDocumentPreview>> PreviewAsync(
            string? docId = null,
            CancellationToken cancellationToken = default)
        {
            var read = await ReadAllAsync(cancellationToken);

            // Lọc SAU khi đã đọc cả thư mục, không phải trước: phân quyền của tài liệu bối cảnh cần
            // danh sách NPC dựng từ mọi tài liệu khác, nên xem trước một file mà chỉ đọc một file sẽ
            // cho ra bảng quyền khác với lúc nạp thật — đúng loại sai lệch mà một bản xem trước
            // sinh ra để loại bỏ.
            return read
                .Where(entry => docId is null || string.Equals(entry.DocId, docId, StringComparison.Ordinal))
                .Select(entry => new CorpusDocumentPreview(
                    entry.DocId,
                    entry.FileName,
                    entry.NpcNames,
                    entry.Content.Metadata,
                    _chunker.Chunk(entry.Content.Body)
                            .Select(segment => new CorpusChunkPreview(
                                segment.Ordinal is { } ordinal ? ChunkCodes.Build(entry.DocId, ordinal) : string.Empty,
                                segment.Text))
                            .ToList()))
                .ToList();
        }

        /// <summary>
        /// Đọc cả thư mục rồi giải phân quyền một lượt. Dùng chung cho mọi đường CHỈ ĐỌC, để bảng
        /// quyền mà người ta soi trước khi nạp là đúng bảng quyền sẽ được ghi xuống.
        /// </summary>
        private async Task<List<ReadDocument>> ReadAllAsync(CancellationToken cancellationToken)
        {
            var metadata = new List<IReadOnlyDictionary<string, string>>();
            var files = new List<(string FileName, ExtractedDocument Content)>();

            foreach (var path in CorpusFiles())
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();
                var extractor = _extractors.FirstOrDefault(candidate => candidate.Supports(extension));

                if (extractor is null)
                    continue;

                await using var stream = File.OpenRead(path);

                var content = await extractor.ExtractAsync(stream, cancellationToken);

                files.Add((Path.GetFileName(path), content));
                metadata.Add(content.Metadata);
            }

            var access = _accessPolicy.Resolve(metadata);

            return files
                .Select((file, index) => new ReadDocument(
                    access[index].DocId.Length > 0
                        ? access[index].DocId
                        : Path.GetFileNameWithoutExtension(file.FileName),
                    file.FileName,
                    access[index].NpcNames,
                    file.Content))
                .ToList();
        }

        private sealed record ReadDocument(string DocId,
                                           string FileName,
                                           IReadOnlyList<string> NpcNames,
                                           ExtractedDocument Content);

        /// <summary>
        /// Danh sách file corpus, sắp theo tên.
        /// <para>
        /// Thư mục không tồn tại thì ném <see cref="DirectoryNotFoundException"/> thay vì trả danh
        /// sách rỗng: "nạp xong, 0 file" là một câu trả lời trông như thành công cho một cấu hình
        /// hỏng, và người bấm sẽ đi tìm lỗi ở chỗ khác.
        /// </para>
        /// </summary>
        private List<string> CorpusFiles()
        {
            // ContentFilePath chứ KHÔNG phải AppDataPath: corpus là nguồn được <Content> chép sang
            // thư mục output, nên ở máy dev nó nằm cạnh RAG.dll chứ không nằm trong thư mục project.
            var directory = ContentFilePath.Resolve(_config.CorpusPath);

            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);

            return Directory.GetFiles(directory)
                            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                            .ToList();
        }
    }
}
