using RAG.Class.Validation;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class
{
    /// <summary>
    /// Câu hỏi của người chơi gửi tới một NPC cụ thể.
    /// <para>
    /// Ràng buộc khai báo bằng attribute chứ không kiểm tay trong action: <c>[ApiController]</c>
    /// tự trả 400 kèm ProblemDetails, nên không còn chuỗi thông báo lỗi nào nằm trong controller.
    /// </para>
    /// </summary>
    /// <param name="NpcName">Tên NPC; đồng thời là khóa lọc payload khi truy hồi.</param>
    /// <param name="NpcSystem">Mô tả tính cách NPC, ghép vào system prompt.</param>
    /// <param name="Question">Câu hỏi gốc, chưa chuẩn hóa.</param>
    /// <param name="Summary">
    /// Tùy chọn: tóm tắt phần hội thoại cũ, lấy từ <c>api/query/compact-history</c>.
    /// </param>
    /// <param name="History">
    /// Tùy chọn: các lượt gần nhất, cũ trước mới sau, KHÔNG gồm câu hỏi hiện tại. Bỏ trống cả hai
    /// trường thì hành vi y hệt trước khi có tính năng lịch sử — client cũ không phải sửa gì.
    /// </param>
    /// <param name="Stage">
    /// Tùy chọn: mã số giai đoạn mạch chuyện hiện tại của NPC (1 = chưa có bằng chứng,
    /// 2 = đã có bằng chứng), do client tự xác định và gửi lên. Bỏ trống thì không lọc theo stage — hành vi y hệt trước khi
    /// có tính năng này, client cũ không phải sửa gì. Chỉ có ý nghĩa với NPC có tri thức gắn
    /// tiền tố <c>[stage:...]</c> trong corpus hoặc có <c>WeakPoint:Targets</c> khai riêng theo
    /// <c>Stage</c>; NPC khác nhận field này mà bỏ qua, không lỗi.
    /// </param>
    public record RequestDto(
        [NotBlank] string NpcName,
        string NpcSystem,
        [NotBlank] string Question,
        string? Summary = null,
        List<ConversationTurnDto>? History = null,
        int? Stage = null)
    {
        public string? StageName => StageCatalog.Resolve(Stage);

        public ConversationHistory ToConversationHistory() =>
            ConversationDtoMapper.ToConversationHistory(NpcName, Summary, History);
    }
}
