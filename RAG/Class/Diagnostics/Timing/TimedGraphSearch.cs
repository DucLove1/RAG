using System.Globalization;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo nhánh đồ thị (trừ phần tra nguyên văn).
    /// <para>
    /// Nhãn <c>edges</c> đi kèm vì cùng lý do với <c>hits</c> của kho vector: một lượt 800ms ra 0 cạnh
    /// và một lượt 800ms ra 16 cạnh là hai tình huống khác hẳn nhau. Với Null Object (đồ thị tắt)
    /// stage này gần như bằng 0 — bọc vẫn vô hại.
    /// </para>
    /// </summary>
    public sealed class TimedGraphSearch : IGraphSearch
    {
        private readonly IGraphSearch _inner;
        private readonly ILatencyTracker _latency;

        public TimedGraphSearch(IGraphSearch inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public async Task<GraphContext> SearchAsync(GraphSearchQuery query, CancellationToken cancellationToken = default)
        {
            var context = await _latency.TrackAsync(
                LatencyStages.GraphSearch, () => _inner.SearchAsync(query, cancellationToken));

            _latency.Tag(LatencyTags.Edges, context.Relations.Count.ToString(CultureInfo.InvariantCulture));

            return context;
        }
    }
}
