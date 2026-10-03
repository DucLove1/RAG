using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Mở phiên đo riêng cho endpoint compact và đo lượt gọi LLM tóm tắt. Phiên riêng chứ không gộp
    /// vào <c>ask</c>: một lượt tóm tắt dài hơn hẳn một câu thoại, gộp chung thì phân bố "ask chậm"
    /// sẽ bị kéo lệch.
    /// </summary>
    public sealed class TimedConversationCompactor : IConversationCompactor
    {
        private readonly IConversationCompactor _inner;
        private readonly ILatencyTracker _latency;
        private readonly ILatencySessionFactory _sessions;

        public TimedConversationCompactor(IConversationCompactor inner,
                                          ILatencyTracker latency,
                                          ILatencySessionFactory sessions)
        {
            _inner = inner;
            _latency = latency;
            _sessions = sessions;
        }

        public async Task<string> CompactAsync(ConversationHistory history, CancellationToken cancellationToken = default)
        {
            using var session = _sessions.Begin(LatencyOperations.CompactHistory);

            // Chỉ tên NPC, không phải nội dung hội thoại — cùng lý do với TimedAskService.
            _latency.Tag(LatencyTags.Npc, history.NpcName);

            return await _latency.TrackAsync(LatencyStages.Compaction,
                () => _inner.CompactAsync(history, cancellationToken));
        }
    }
}
