using System.Runtime.CompilerServices;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Gốc của phiên đo cho đường trả lời theo LUỒNG. Song song với <see cref="TimedAskService"/>,
    /// nhưng khác nó ở một điểm sống còn được giải thích ngay dưới đây.
    /// </summary>
    public sealed class TimedAskStreamService : IAskStreamService
    {
        private readonly IAskStreamService _inner;
        private readonly ILatencyTracker _latency;
        private readonly ILatencySessionFactory _sessions;

        public TimedAskStreamService(IAskStreamService inner,
                                     ILatencyTracker latency,
                                     ILatencySessionFactory sessions)
        {
            _inner = inner;
            _latency = latency;
            _sessions = sessions;
        }

        /// <summary>
        /// PHẢI là một iterator (<c>async IAsyncEnumerable</c>), không phải một method thường trả
        /// thẳng luồng của lớp bên trong.
        /// <para>
        /// Method thường sẽ return NGAY LẬP TỨC — trước khi consumer duyệt lấy một mảnh nào — nên
        /// <c>using var session</c> chốt sổ khi chưa có gì xảy ra, và mọi con số trong log thành
        /// rác: tổng gần 0ms, không một stage nào. Là iterator thì thân method chỉ chạy khi consumer
        /// gọi <c>MoveNextAsync</c>, và <c>using</c> sống đúng bằng vòng đời của cả luồng.
        /// </para>
        /// <para>
        /// <see cref="EnumeratorCancellationAttribute"/> cũng là bắt buộc: thiếu nó, token mà tầng
        /// ghi ra dây truyền vào qua <c>WithCancellation</c> bị bỏ qua ÂM THẦM, và người chơi ngắt
        /// kết nối sẽ không bao giờ xuống được tới provider — request vẫn chạy hết và vẫn đốt hạn
        /// mức API cho một câu trả lời không ai đọc.
        /// </para>
        /// </summary>
        public async IAsyncEnumerable<AskStreamEvent> AskStreamAsync(
            string npcName,
            string npcSystem,
            string question,
            int topK,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var session = _sessions.Begin(LatencyOperations.AskStream);

            // Chỉ tên NPC, KHÔNG phải câu hỏi hay persona — cùng lý do đã ghi ở TimedAskService.
            _latency.Tag(LatencyTags.Npc, npcName);

            // try/finally chứ không phải sau vòng lặp: một luồng bị người chơi ngắt giữa chừng, hay
            // một luồng ném ra, vẫn phải để lại nhãn nhánh. Nếu không thì đúng những request đáng
            // điều tra nhất lại là những request thiếu thông tin nhất.
            //
            // yield return nằm trong try có FINALLY là hợp lệ; chỉ try có CATCH mới bị C# cấm.
            try
            {
                await foreach (var streamEvent in _inner
                    .AskStreamAsync(npcName, npcSystem, question, topK, cancellationToken)
                    .WithCancellation(cancellationToken))
                {
                    yield return streamEvent;

                    // Ngay sau yield return là ĐẦU của lần MoveNextAsync kế tiếp, và phiên hiện
                    // hành vừa biến mất cùng ExecutionContext của lần trước. Không có dòng này thì
                    // mọi stage sinh ra từ đây trở đi — llmFirstToken, llmAnswer, answerCacheSet —
                    // rơi khỏi báo cáo, mà dòng log VẪN xuất hiện với tổng thời gian đúng nên
                    // trông y như những stage đó chạy tức thì. Xem ILatencySession.Activate.
                    session.Activate();
                }
            }
            finally
            {
                // finally chạy trong DisposeAsync, tức là lại một lần vào mới nữa.
                session.Activate();
                _latency.Tag(LatencyTags.Branch, AskBranchResolver.Resolve(_latency));
            }
        }
    }
}
