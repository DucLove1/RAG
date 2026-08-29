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
        private readonly IQueryCache _queryCache;

        public QueryController(RAGPipline ragPipline, RagConfig config, IQueryCache queryCache)
        {
            _ragPipline = ragPipline;
            _config = config;
            _queryCache = queryCache;
        }

        [HttpPost("ask")]
        async public Task<IActionResult> Post([FromBody] RequestDto request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                return BadRequest();

            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest("Question cannot be empty.");

            if (string.IsNullOrWhiteSpace(request.npcName))
                return BadRequest("NPC Name cannot be empty.");

            var response = await _ragPipline.AskAsync(request.npcName, request.npcSystem, request.Question, _config.TopK, cancellationToken);
            return Ok(response);
        }

        [HttpPost("upload")]
        async public Task<IActionResult> PostEmbedding([FromForm] List<IFormFile> files, [FromForm] string npcNames, CancellationToken cancellationToken = default)
        {
            if (files == null || files.Count == 0)
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

                foreach (var chunk in TextChunker.ChunkText(raw, _config.ChunkSize, _config.ChunkOverlap))
                {
                    chunks.Add((npcNames, chunk, file.FileName));
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
                    Console.WriteLine($"Chunked text from {file.FileName}: {chunk}");
                }
            }

            if (chunks.Count == 0)
                return BadRequest("No valid files uploaded.");

            await _ragPipline.IngestAsync(chunks, cancellationToken);
            return Ok();
        }

        [HttpPost("create-collection")]
        async public Task<IActionResult> PostCreateCollection(CancellationToken cancellationToken = default)
        {
            await _ragPipline.CreateCollection(cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Chẩn đoán định tuyến: trả về điểm của mọi route cho một câu hỏi.
        /// Không gọi LLM và không chạm Qdrant, nên đây là vòng lặp rẻ để tinh chỉnh ngưỡng.
        /// </summary>
        [HttpPost("route-debug")]
        async public Task<IActionResult> PostRouteDebug([FromBody] RouteDebugRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
                return BadRequest("Question cannot be empty.");

            var (normalizedQuestion, scores, match) = await _ragPipline.ExplainRouteAsync(request.Question, cancellationToken);

            return Ok(new RouteDebugResponse(request.Question, normalizedQuestion, match?.Name, scores));
        }

        /// <summary>
        /// Thêm câu mẫu vào một route đang chạy. Nhận câu dạng text (sẽ được nhúng),
        /// vector đã chuẩn bị sẵn, hoặc cả hai. Có hiệu lực ngay, không cần khởi động lại.
        /// </summary>
        [HttpPost("route-utterances")]
        async public Task<IActionResult> PostRouteUtterances([FromBody] AddUtterancesRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Route))
                return BadRequest("Route cannot be empty.");

            var utterances = request.Utterances ?? new List<string>();
            var vectors = request.Vectors ?? new List<float[]>();

            if (utterances.Count == 0 && vectors.Count == 0)
                return BadRequest("Phải cung cấp ít nhất một câu mẫu hoặc một vector.");

            var result = await _ragPipline.AddRouteUtterancesAsync(request.Route, utterances, vectors, cancellationToken);

            // Không thêm được vì tên route sai hay dữ liệu không hợp lệ là lỗi của người gọi,
            // nên trả 400 thay vì 200 kèm success=false.
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Tỉ lệ trúng cache. Không có số liệu này thì không cách nào biết cache đang thực sự
        /// tiết kiệm được gì hay chỉ đang chiếm RAM.
        /// </summary>
        [HttpGet("cache-stats")]
        public IActionResult GetCacheStats()
        {
            var stats = _queryCache.GetStats();

            return Ok(new
            {
                normalization = new
                {
                    hits = stats.NormalizationHits,
                    misses = stats.NormalizationMisses,
                    hitRate = Math.Round(stats.NormalizationHitRate, 3)
                },
                embedding = new
                {
                    hits = stats.EmbeddingHits,
                    misses = stats.EmbeddingMisses,
                    hitRate = Math.Round(stats.EmbeddingHitRate, 3)
                }
            });
        }

        [HttpGet("check-health")]
        async public Task<IActionResult> GetCheckHealth(CancellationToken cancellationToken = default)
        {
            return Ok();
        }
    }
}
