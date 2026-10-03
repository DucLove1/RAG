using Microsoft.AspNetCore.Mvc;
using RAG.Class;
using RAG.Interface;

namespace RAG.Controllers
{
    /// <summary>
    /// Quản lý lịch sử hội thoại phía client. Tách khỏi <see cref="QueryController"/> vì đây không phải
    /// đường trả lời: không truy hồi, không định tuyến, chỉ gộp lịch sử thành tóm tắt.
    /// </summary>
    [Route("api/query")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly IConversationCompactor _compactor;

        public ConversationController(IConversationCompactor compactor) => _compactor = compactor;

        /// <summary>
        /// Gộp đoạn lịch sử client gửi lên (kèm tóm tắt cũ nếu có) thành MỘT chuỗi tóm tắt.
        /// Server không lưu gì: client thay các lượt đã gộp bằng tóm tắt này và gửi nó ở trường
        /// <c>summary</c> của lần hỏi sau. Client tự quyết định khi nào gọi.
        /// </summary>
        [HttpPost("compact-history")]
        public async Task<IActionResult> PostCompactHistory([FromBody] CompactHistoryRequest request,
                                                            CancellationToken cancellationToken = default)
        {
            var history = ConversationDtoMapper.ToConversationHistory(
                request.NpcName, request.PreviousSummary, request.History);

            var summary = await _compactor.CompactAsync(history, cancellationToken);

            return Ok(new CompactHistoryResponse(summary));
        }
    }
}
