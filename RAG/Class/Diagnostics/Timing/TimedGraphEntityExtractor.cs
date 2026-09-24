using System.Globalization;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo lượt LLM trích thực thể.
    /// <para>
    /// Cần decorator riêng vì provider LLM mà bộ trích dùng là bản KEYED, và tầng đo cố ý không bọc
    /// provider keyed (xem <c>TimedLlmProvider</c>) — không có stage này thì lượt gọi nằm trên đường
    /// nóng mà không ai thấy. Nhãn số lượng cho biết một lượt trích chậm có mang về gì không.
    /// </para>
    /// </summary>
    public sealed class TimedGraphEntityExtractor : IGraphEntityExtractor
    {
        private readonly IGraphEntityExtractor _inner;
        private readonly ILatencyTracker _latency;

        public TimedGraphEntityExtractor(IGraphEntityExtractor inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public async Task<GraphExtraction> ExtractAsync(string npcName,
                                                        string question,
                                                        CancellationToken cancellationToken = default)
        {
            var extraction = await _latency.TrackAsync(
                LatencyStages.GraphExtract, () => _inner.ExtractAsync(npcName, question, cancellationToken));

            // Chỉ ghi SỐ LƯỢNG, không ghi tên thực thể: tên là nội dung vụ án, và log của server game
            // thường được nhiều người xem hơn là người chơi.
            _latency.Tag(LatencyTags.Entities, extraction.Entities.Count.ToString(CultureInfo.InvariantCulture));
            _latency.Tag(LatencyTags.Intents, extraction.RelationTypes.Count.ToString(CultureInfo.InvariantCulture));

            return extraction;
        }
    }
}
