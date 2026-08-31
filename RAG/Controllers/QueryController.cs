using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAG.Class;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Controllers
{
    /// <summary>
    /// Đường trả lời câu hỏi của người chơi.
    /// <para>
    /// Chỉ nhận <see cref="IAskService"/> chứ không nhận cả façade: controller này không có cách nào
    /// gọi nhầm sang đường nạp dữ liệu hay quản trị route.
    /// </para>
    /// </summary>
    [Route("api/query")]
    [ApiController]
    public class QueryController : ControllerBase
    {
        private readonly IAskService _askService;
        private readonly RagConfig _config;

        public QueryController(IAskService askService, IOptions<RagConfig> config)
        {
            _askService = askService;
            _config = config.Value;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Post([FromBody] RequestDto request, CancellationToken cancellationToken = default)
        {
            var response = await _askService.AskAsync(
                request.NpcName, request.NpcSystem, request.Question, _config.TopK, cancellationToken);

            return Ok(response);
        }

        [HttpGet("check-health")]
        public IActionResult GetCheckHealth() => Ok();
    }
}
