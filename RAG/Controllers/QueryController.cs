using Microsoft.AspNetCore.Mvc;
using RAG.Class;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;

namespace RAG.Controllers
{
    [Route("api/query")]
    [ApiController]
    public class QueryController : Controller
    {
        private readonly RAGPipline _ragPipline;
        private readonly RagConfig _config;

        public QueryController(RAGPipline ragPipline, RagConfig config)
        {
            _ragPipline = ragPipline;
            _config = config;
        }

        [HttpPost("ask")]
        async public Task<IActionResult> Post([FromBody] RequestDto request, CancellationToken cancellationToken)
        {
            if (request == null)
                return BadRequest();

            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest("Question cannot be empty.");

            if (string.IsNullOrWhiteSpace(request.npcName))
                return BadRequest("NPC Name cannot be empty.");

            var response = await _ragPipline.AskAsync(request.npcName, request.npcSystem, request.Question, _config.topK, cancellationToken);
            return Ok(response);
        }

        [HttpPost("upload")]
        async public Task<IActionResult> PostEmbedding([FromForm] List<IFormFile> files, [FromForm] string npcNames, CancellationToken cancellationToken)
        {
            if(files == null || files.Count == 0)
                return BadRequest();

            var chunks = new List<(string npcNames, string text, string? source)>();
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (ext is not (".pdf" or ".txt" or ".md" or ".json"))
                    continue;

                var tmp = Path.GetTempFileName();
                await using (var fileStream = new FileStream(tmp, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream, cancellationToken);
                }

                string raw = ext switch
                {
                    ".txt" or ".md" or ".json" => await System.IO.File.ReadAllTextAsync(tmp, cancellationToken),
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(raw))
                    continue;

                foreach(var chunk in TextChunker.ChunkText(raw, _config.chunkSize, _config.chunkOverlap))
                {
                    chunks.Add((npcNames, chunk, file.FileName));
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
                    Console.WriteLine($"Chunked text from {file.FileName}: {chunk}");
                }
            }

            if(chunks.Count == 0)
                return BadRequest("No valid files uploaded.");

            await _ragPipline.IngestAsync(chunks, cancellationToken);
            return Ok();
        }

        [HttpPost("create-collection")]
        async public Task<IActionResult> PostCreateCollection(CancellationToken cancellationToken)
        {
            await _ragPipline.CreateCollection(cancellationToken);
            return Ok();
        }
    }
}
