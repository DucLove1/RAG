using RAG.Class.Constants;
using RAG.Class.Validation;
using RAG.Interface;
using System.ComponentModel.DataAnnotations;

namespace RAG.Class
{
    /// <summary>Một lượt hội thoại trên dây; <c>role</c> nhận <c>"player"</c> hoặc <c>"npc"</c>.</summary>
    public record ConversationTurnDto(ConversationRole Role, string Content);

    /// <summary>
    /// Yêu cầu gộp một đoạn lịch sử thành tóm tắt. Client gửi đúng phần CẦN gộp (thường là các lượt
    /// cũ), kèm tóm tắt của lần gộp trước nếu có, rồi giữ lại vài lượt gần nhất nguyên văn.
    /// </summary>
    /// <param name="PreviousSummary">Tóm tắt của lần compact trước; được gộp vào bản mới.</param>
    /// <param name="History">Các lượt cần gộp, cũ trước mới sau. Phải có ít nhất một lượt.</param>
    public record CompactHistoryRequest(
        [NotBlank] string NpcName,
        string? PreviousSummary,
        [Required, MinLength(1)] List<ConversationTurnDto> History);

    /// <summary>Tóm tắt đã gộp; client gửi lại nó ở trường <c>summary</c> của lần hỏi sau.</summary>
    public record CompactHistoryResponse(string Summary);

    /// <summary>Chuyển dữ liệu trên dây sang mô hình miền <see cref="ConversationHistory"/>.</summary>
    public static class ConversationDtoMapper
    {
        /// <summary>Không có tóm tắt lẫn lượt nào thì trả <see cref="ConversationHistory.Empty"/>.</summary>
        public static ConversationHistory ToConversationHistory(string npcName,
                                                                string? summary,
                                                                IReadOnlyList<ConversationTurnDto>? turns)
        {
            if (string.IsNullOrWhiteSpace(summary) && (turns is null || turns.Count == 0))
                return ConversationHistory.Empty;

            var mapped = (turns ?? Array.Empty<ConversationTurnDto>())
                .Select(turn => new ConversationTurn(turn.Role, turn.Content ?? string.Empty))
                .ToList();

            return new ConversationHistory(npcName, summary, mapped);
        }
    }
}
