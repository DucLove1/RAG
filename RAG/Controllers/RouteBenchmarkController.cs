using Microsoft.AspNetCore.Mvc;
using RAG.Interface;

namespace RAG.Controllers
{
    /// <summary>
    /// So sánh các chiến lược định tuyến trên bộ câu hỏi cố định (RouteBenchmark:CasesPath).
    /// Chỉ chạy bước chuẩn hóa + định tuyến; không truy hồi, không sinh câu trả lời.
    /// </summary>
    [Route("api/query")]
    [ApiController]
    public class RouteBenchmarkController : ControllerBase
    {
        private readonly IRouteBenchmark _benchmark;

        public RouteBenchmarkController(IRouteBenchmark benchmark)
        {
            _benchmark = benchmark;
        }

        [HttpPost("route-benchmark")]
        public async Task<IActionResult> PostRouteBenchmark([FromQuery] bool onlyMistakes = false,
                                                            CancellationToken cancellationToken = default) =>
            Ok(await _benchmark.RunAsync(onlyMistakes, cancellationToken));
    }
}
