using Microsoft.AspNetCore.Mvc;
using RAG.Interface;

namespace RAG.Controllers
{
    /// <summary>
    /// Đường nạp tri thức vào kho vector. Tách khỏi đường trả lời vì hai bên có nhịp thay đổi
    /// hoàn toàn khác nhau và gần như chắc chắn sẽ cần chính sách bảo vệ khác nhau.
    /// </summary>
    [Route("api/query")]
    [ApiController]
    public class IngestionController : ControllerBase
    {
        private readonly IIngestionService _ingestionService;
        private readonly ICorpusIngestionService _corpusIngestionService;
        private readonly IChunkTextLookup _chunkTextLookup;

        public IngestionController(IIngestionService ingestionService,
                                   ICorpusIngestionService corpusIngestionService,
                                   IChunkTextLookup chunkTextLookup)
        {
            _ingestionService = ingestionService;
            _corpusIngestionService = corpusIngestionService;
            _chunkTextLookup = chunkTextLookup;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> PostEmbedding([FromForm] List<IFormFile> files,
                                                       [FromForm] string npcNames,
                                                       CancellationToken cancellationToken = default)
        {
            if (files is null || files.Count == 0)
                return BadRequest();

            // Đọc thẳng từ stream của request. Bản trước ghi ra Path.GetTempFileName() rồi đọc lại
            // mà KHÔNG BAO GIỜ xoá — mỗi lần upload để lại một file rác trong thư mục temp.
            var streams = new List<Stream>(files.Count);

            try
            {
                var documents = new List<DocumentSource>(files.Count);

                foreach (var file in files)
                {
                    var stream = file.OpenReadStream();
                    streams.Add(stream);
                    documents.Add(new DocumentSource(file.FileName, stream));
                }

                var result = await _ingestionService.IngestAsync(documents, npcNames, cancellationToken);

                // Không đoạn nào vào được kho là thất bại của người gọi (sai định dạng, file rỗng),
                // nên trả 400 kèm số liệu để họ biết chính xác chuyện gì đã xảy ra.
                return result.ChunksIngested > 0 ? Ok(result) : BadRequest(result);
            }
            finally
            {
                foreach (var stream in streams)
                    await stream.DisposeAsync();
            }
        }

        /// <summary>
        /// Nạp toàn bộ thư mục corpus trong một lệnh: tự bóc front matter, tự sinh mã chunk, tự suy
        /// phân quyền từ trường <c>nguon</c>. Không nhận tham số nào — dựng lại index phải là một
        /// thao tác lặp lại được y hệt, không phụ thuộc vào người bấm gõ đúng những gì.
        /// </summary>
        [HttpPost("ingest-corpus")]
        public async Task<IActionResult> PostIngestCorpus(CancellationToken cancellationToken = default)
        {
            var report = await _corpusIngestionService.IngestCorpusAsync(cancellationToken);

            return report.Result.ChunksIngested > 0 ? Ok(report) : BadRequest(report);
        }

        /// <summary>
        /// Bảng phân quyền suy ra từ corpus, KHÔNG ghi gì. Đây là bảng phải khớp với cột "Nguồn"
        /// trong HUONG_DAN.md; lệch một dòng nghĩa là một NPC đang hoặc sẽ trả lời tri thức của người khác.
        /// </summary>
        [HttpGet("corpus-access")]
        public async Task<IActionResult> GetCorpusAccess(CancellationToken cancellationToken = default) =>
            Ok(await _corpusIngestionService.DescribeAccessAsync(cancellationToken));

        /// <summary>
        /// Cắt đoạn và sinh mã chunk rồi dừng — KHÔNG nhúng, KHÔNG ghi. Đây là chỗ đối chiếu mã
        /// chunk với dòng trong file nguồn, và nó phải miễn phí thì mới có người chịu chạy.
        /// </summary>
        [HttpGet("corpus-preview")]
        public async Task<IActionResult> GetCorpusPreview([FromQuery] string? docId = null,
                                                          CancellationToken cancellationToken = default) =>
            Ok(await _corpusIngestionService.PreviewAsync(docId, cancellationToken));

        /// <summary>
        /// Tra nguyên văn trong KHO VECTOR theo mã chunk, kèm bộ lọc NPC.
        /// <para>
        /// Khác <c>corpus-preview</c> ở chỗ quyết định: bên kia đọc file trên đĩa, bên này đọc thứ
        /// THẬT SỰ đã ghi vào Qdrant. Hai bên khớp nhau nghĩa là khóa ghép đã đi trọn đường từ file
        /// nguồn tới kho vector — đó là điều kiện để đồ thị nối vào được.
        /// </para>
        /// <para>
        /// Kèm <c>npcName</c> vì bản thân bộ lọc quyền cũng là thứ cần kiểm: tra một mã của tài liệu
        /// mà NPC đó không được biết PHẢI trả về rỗng.
        /// </para>
        /// </summary>
        [HttpGet("chunk")]
        public async Task<IActionResult> GetChunk([FromQuery] string code,
                                                  [FromQuery] string npcName,
                                                  CancellationToken cancellationToken = default)
        {
            var found = await _chunkTextLookup.GetByCodesAsync(npcName, new[] { code }, cancellationToken);

            return found.TryGetValue(code, out var chunk) ? Ok(chunk) : NotFound(new { code, npcName });
        }

        [HttpPost("create-collection")]
        public async Task<IActionResult> PostCreateCollection(CancellationToken cancellationToken = default)
        {
            await _ingestionService.CreateCollectionAsync(cancellationToken);
            return Ok();
        }
    }
}
