using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Conversation
{
    /// <summary>
    /// Cắt gọn và dựng lịch sử hội thoại thành văn bản theo template trong
    /// <see cref="ConversationRenderConfig"/>. Không có chữ nào của prompt nằm trong lớp này.
    /// </summary>
    public sealed class ConversationFormatter : IConversationFormatter
    {
        private readonly ConversationConfig _config;

        public ConversationFormatter(IOptions<ConversationConfig> options) => _config = options.Value;

        public ConversationHistory Trim(ConversationHistory history) =>
            TrimLengths(history.TakeLast(_config.MaxTurns));

        public ConversationHistory TrimLengths(ConversationHistory history)
        {
            if (history.IsEmpty)
                return history;

            var turns = history.Turns
                .Where(turn => !string.IsNullOrWhiteSpace(turn.Content))
                .Select(turn => turn with { Content = Cut(turn.Content.Trim(), _config.MaxTurnLength) })
                .ToList();

            var summary = string.IsNullOrWhiteSpace(history.Summary) ? null : TrimSummary(history.Summary);

            return history with { Summary = summary, Turns = turns };
        }

        public string RenderTranscript(ConversationHistory history)
        {
            if (history.IsEmpty)
                return string.Empty;

            var render = _config.Render;
            var lines = new List<string>(history.Turns.Count + 1);

            if (!string.IsNullOrWhiteSpace(history.Summary))
                lines.Add(string.Format(render.SummaryTemplate, history.Summary.Trim()));

            foreach (var turn in history.Turns)
            {
                lines.Add(turn.Role == ConversationRole.Npc
                    ? string.Format(render.NpcLineTemplate, history.NpcName, turn.Content)
                    : string.Format(render.PlayerLineTemplate, turn.Content));
            }

            return string.Join(render.LineSeparator, lines);
        }

        public string PrependToAnswerPrompt(ConversationHistory history, string userPrompt)
        {
            if (history.IsEmpty)
                return userPrompt;

            return string.Format(_config.Render.AnswerBlockTemplate, history.NpcName, RenderTranscript(history)) + userPrompt;
        }

        public string TrimSummary(string summary) => Cut(summary.Trim(), _config.MaxSummaryLength);

        /// <summary>Cắt theo số ký tự mà không tách đôi một cặp surrogate.</summary>
        private static string Cut(string value, int maxLength)
        {
            if (value.Length <= maxLength)
                return value;

            var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
            return value[..length];
        }
    }
}
