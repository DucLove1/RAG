using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo toàn bộ bước dựng ngữ cảnh. Đặt cạnh <c>vectorSearch</c>, <c>graphSearch</c> và
    /// <c>chunkResolve</c> trên cùng một dòng log, con số này cho biết hai nhánh có thật sự chạy song
    /// song hay không — xem <see cref="LatencyStages.ContextBuild"/>.
    /// </summary>
    public sealed class TimedAskContextBuilder : IAskContextBuilder
    {
        private readonly IAskContextBuilder _inner;
        private readonly ILatencyTracker _latency;

        public TimedAskContextBuilder(IAskContextBuilder inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public Task<AskContext> BuildAsync(string npcName,
                                           string question,
                                           float[] questionEmbedding,
                                           int topK,
                                           CancellationToken cancellationToken = default) =>
            _latency.TrackAsync(
                LatencyStages.ContextBuild,
                () => _inner.BuildAsync(npcName, question, questionEmbedding, topK, cancellationToken));
    }
}
