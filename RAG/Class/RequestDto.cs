using RAG.Class.Validation;

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
    public record RequestDto(
        [NotBlank] string NpcName,
        string NpcSystem,
        [NotBlank] string Question);
}
