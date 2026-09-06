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

        /// <summary>
        /// THAY ĐỔI CÓ THỂ PHÁ CLIENT: trước đây endpoint này trả về câu trả lời dưới dạng chuỗi
        /// trần (text/plain với client không gửi Accept: application/json). Nay nó trả về object
        /// <c>{ "answer": "...", "weakPointHit": false }</c> để client đọc được cờ trúng điểm yếu.
        /// Client cũ đọc thẳng body làm lời NPC sẽ hiển thị nguyên cục JSON chứ không báo lỗi.
        /// </summary>
        [HttpPost("ask")]
        public async Task<IActionResult> Post([FromBody] RequestDto request, CancellationToken cancellationToken = default)
        {
            var result = await _askService.AskAsync(
                request.NpcName, request.NpcSystem, request.Question, _config.TopK, cancellationToken);

            return Ok(result);
        }

        [HttpGet("check-health")]
        public IActionResult GetCheckHealth() => Ok();
    }
}
