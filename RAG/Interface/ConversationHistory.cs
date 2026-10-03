using RAG.Class.Constants;

namespace RAG.Interface
{
    /// <summary>Một lượt nói trong hội thoại giữa người chơi và NPC.</summary>
    public sealed record ConversationTurn(ConversationRole Role, string Content);

    /// <summary>
    /// Lịch sử hội thoại mà CLIENT gửi kèm câu hỏi — server không lưu trạng thái phiên nào.
    /// <para>
    /// Mang theo <see cref="NpcName"/> vì lịch sử là cuộc trò chuyện với MỘT NPC cụ thể: dựng các
    /// lượt của NPC thành văn bản cần tên đó, và nhờ vậy những node như bộ chuẩn hóa không phải nhận
    /// thêm một tham số tên NPC chỉ để phục vụ việc dựng.
    /// </para>
    /// <para>
    /// Null Object: request không gửi lịch sử nhận <see cref="Empty"/> chứ không phải <c>null</c>,
    /// nên không node nào phải kiểm <c>null</c>.
    /// </para>
    /// </summary>
    /// <param name="Summary">Bản tóm tắt phần hội thoại cũ, do endpoint compact sinh ra.</param>
    /// <param name="Turns">Các lượt gần nhất còn giữ nguyên văn, cũ trước mới sau.</param>
    public sealed record ConversationHistory(string NpcName, string? Summary, IReadOnlyList<ConversationTurn> Turns)
    {
        public static ConversationHistory Empty { get; } = new(string.Empty, null, Array.Empty<ConversationTurn>());

        /// <summary>Không có gì để đưa vào prompt: không tóm tắt và không lượt nào.</summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Summary) && Turns.Count == 0;

        /// <summary>Giữ tóm tắt và <paramref name="count"/> lượt CUỐI.</summary>
        public ConversationHistory TakeLast(int count) =>
            Turns.Count <= count ? this : this with { Turns = Turns.Skip(Turns.Count - count).ToList() };
    }
}
