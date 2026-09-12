using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Gốc của phiên đo cho đường trả lời: mở phiên, và chốt sổ bằng việc suy ra nhánh nào đã chạy.
    /// <para>
    /// Nhánh KHÔNG được <c>AskPipeline</c> khai báo — nó được suy ra từ chính các nhãn mà những
    /// decorator ở dưới đã gắn dọc đường (xem <see cref="AskBranchResolver"/>). Đó là cách duy nhất
    /// biết được nhánh mà vẫn giữ lớp lõi hoàn toàn không biết gì về việc đo giờ.
    /// </para>
    /// </summary>
    public sealed class TimedAskService : IAskService
    {
        private readonly IAskService _inner;
        private readonly ILatencyTracker _latency;
        private readonly ILatencySessionFactory _sessions;

        public TimedAskService(IAskService inner, ILatencyTracker latency, ILatencySessionFactory sessions)
        {
            _inner = inner;
            _latency = latency;
            _sessions = sessions;
        }

        public async Task<AskResult> AskAsync(string npcName,
                                              string npcSystem,
                                              string question,
                                              int topK,
                                              CancellationToken cancellationToken = default)
        {
            using var session = _sessions.Begin(LatencyOperations.Ask);

            // Chỉ tên NPC, KHÔNG phải câu hỏi hay persona: câu hỏi của người chơi là dữ liệu do
            // client gửi lên, đưa nguyên văn vào log là vừa phình log vừa lưu thứ không cần lưu.
            // Muốn xem câu hỏi thì đã có sẵn các dòng log của bộ chuẩn hóa và của cache ngữ nghĩa.
            _latency.Tag(LatencyTags.Npc, npcName);

            // finally chứ không phải sau lời gọi: request ném ra vẫn phải để lại nhãn nhánh, nếu
            // không thì đúng những request đáng điều tra nhất lại là những request thiếu thông tin nhất.
            try
            {
                return await _inner.AskAsync(npcName, npcSystem, question, topK, cancellationToken);
            }
            finally
            {
                _latency.Tag(LatencyTags.Branch, AskBranchResolver.Resolve(_latency));
            }
        }
    }
}
