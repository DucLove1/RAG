using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAG.Class;
using RAG.Class.Config;
using RAG.Extension.Sse;
using RAG.Interface;

namespace RAG.Controllers
{
    /// <summary>
    /// Đường trả lời câu hỏi của người chơi.
    /// <para>
    /// Chỉ nhận hai vai trò TRẢ LỜI chứ không nhận cả façade: controller này không có cách nào gọi
    /// nhầm sang đường nạp dữ liệu hay quản trị route.
    /// </para>
    /// </summary>
    [Route("api/query")]
    [ApiController]
    public class QueryController : ControllerBase
    {
        private readonly IAskService _askService;
        private readonly IAskStreamService _askStreamService;
        private readonly IRagErrorMapper _errorMapper;
        private readonly RagConfig _config;
        private readonly ILogger<QueryController> _logger;

        public QueryController(IAskService askService,
                               IAskStreamService askStreamService,
                               IRagErrorMapper errorMapper,
                               IOptions<RagConfig> config,
                               ILogger<QueryController> logger)
        {
            _askService = askService;
            _askStreamService = askStreamService;
            _errorMapper = errorMapper;
            _config = config.Value;
            _logger = logger;
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

        /// <summary>
        /// Bản streaming của <see cref="Post"/>: cùng câu trả lời, nhưng đi ra từng mảnh qua
        /// Server-Sent Events để người chơi thấy chữ chạy dần thay vì chờ cả câu.
        /// <para>
        /// Endpoint RIÊNG chứ không đổi <c>ask</c>: không có cách nào để một client đang đọc
        /// <c>{ answer, weakPointHit }</c> tự hiểu được một luồng nhiều mảnh, nên đổi tại chỗ là
        /// phá mọi client cùng lúc. Giữ cả hai còn để lại một đường không streaming dùng làm mốc
        /// đối chiếu khi nghi ngờ luồng trả sai.
        /// </para>
        /// <para>
        /// Chuỗi sự kiện: <c>meta</c> (mang <c>weakPointHit</c>) rồi <c>token</c>* rồi
        /// <c>done</c> hoặc <c>error</c>. Số token có thể bằng 0. Lỗi xảy ra TRƯỚC mảnh đầu vẫn là
        /// ProblemDetails với mã đúng, hệt <c>ask</c>; chi tiết ở <see cref="AskStreamSseResult"/>.
        /// </para>
        /// <para>
        /// Trả <see cref="IActionResult"/> tự viết chứ không trả thẳng <c>IAsyncEnumerable</c>: bộ
        /// định dạng mặc định của MVC sẽ gói cả luồng thành MỘT mảng JSON, tức là chờ tới token
        /// cuối rồi mới gửi — im lặng xoá sạch lợi ích của cả tính năng.
        /// </para>
        /// </summary>
        [HttpPost("ask-stream")]
        public IActionResult PostStream([FromBody] RequestDto request, CancellationToken cancellationToken = default)
        {
            var events = _askStreamService.AskStreamAsync(
                request.NpcName, request.NpcSystem, request.Question, _config.TopK, cancellationToken);

            return new AskStreamSseResult(events, _errorMapper, _logger);
        }

        [HttpGet("check-health")]
        public IActionResult GetCheckHealth() => Ok();
    }
}
