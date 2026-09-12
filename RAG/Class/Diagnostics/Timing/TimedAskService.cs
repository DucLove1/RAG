using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Gốc của phiên đo cho đường trả lời: mở phiên, và chốt sổ bằng việc suy ra nhánh nào đã chạy.
    /// <para>
    /// Nhánh KHÔNG được <c>AskPipeline</c> khai báo — nó được suy ra từ chính các nhãn mà những
    /// decorator ở dưới đã gắn dọc đường. Đó là cách duy nhất biết được nhánh mà vẫn giữ lớp lõi
    /// hoàn toàn không biết gì về việc đo giờ.
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
                _latency.Tag(LatencyTags.Branch, ResolveBranch());
            }
        }

        /// <summary>
        /// Thứ tự kiểm PHẢI khớp thứ tự thoát sớm của <c>AskPipeline</c>: khớp route thì thoát ngay
        /// và bộ phát hiện điểm yếu KHÔNG chạy, nên nhãn điểm yếu lúc đó vắng mặt chứ không phải
        /// bằng false. Đảo hai nhánh này thì một câu tán gẫu sẽ bị gán nhầm là truy hồi.
        /// </summary>
        private string ResolveBranch()
        {
            if (_latency.GetTag(LatencyTags.WeakPointHit) == LatencyTagValues.True)
                return LatencyTagValues.BranchWeakPoint;

            var route = _latency.GetTag(LatencyTags.Route);

            if (route is not null && route != LatencyTagValues.None)
                return LatencyTagValues.BranchRouted;

            return LatencyTagValues.BranchRetrieval;
        }
    }
}
