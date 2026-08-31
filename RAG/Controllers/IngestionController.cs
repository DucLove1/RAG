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

        public IngestionController(IIngestionService ingestionService)
        {
            _ingestionService = ingestionService;
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

        [HttpPost("create-collection")]
        public async Task<IActionResult> PostCreateCollection(CancellationToken cancellationToken = default)
        {
            await _ingestionService.CreateCollectionAsync(cancellationToken);
            return Ok();
        }
    }
}
