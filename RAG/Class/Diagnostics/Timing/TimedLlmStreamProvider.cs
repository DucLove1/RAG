using System.Diagnostics;
using System.Runtime.CompilerServices;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Đo lượt gọi LLM SINH CÂU TRẢ LỜI ở đường streaming, bằng HAI con số thay vì một.
    /// <para>
    /// <c>llmFirstToken</c> là khoảng người chơi ngồi nhìn màn hình trống, và là con số duy nhất
    /// chứng minh streaming có tác dụng — tổng thời gian sinh chữ thì không đổi. <c>llmAnswer</c>
    /// cố ý dùng lại đúng tên stage của đường không streaming để hai dòng log so được thẳng cột.
    /// </para>
    /// <para>
    /// CHỈ bọc đăng ký <see cref="ILLMStreamProvider"/> KHÔNG KHÓA, cùng lý do đã ghi trong
    /// <see cref="TimedLlmProvider"/>: bọc cả các provider keyed thì lượt gọi bị tính hai lần.
    /// </para>
    /// </summary>
    public sealed class TimedLlmStreamProvider : ILLMStreamProvider
    {
        private readonly ILLMStreamProvider _inner;
        private readonly ILatencyTracker _latency;

        public TimedLlmStreamProvider(ILLMStreamProvider inner, ILatencyTracker latency)
        {
            _inner = inner;
            _latency = latency;
        }

        public async IAsyncEnumerable<string> AskStreamAsync(
            string system,
            string user,
            string? model = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var isFirstChunk = true;

            try
            {
                await foreach (var chunk in _inner
                    .AskStreamAsync(system, user, model, cancellationToken)
                    .WithCancellation(cancellationToken))
                {
                    if (isFirstChunk)
                    {
                        _latency.Record(LatencyStages.LlmFirstToken,
                                        Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                        isFirstChunk = false;
                    }

                    yield return chunk;
                }
            }
            finally
            {
                // finally chứ không phải sau vòng lặp: một luồng bị người chơi ngắt giữa chừng
                // chính là luồng đáng xem nhất, và nó phải để lại con số thay vì biến mất khỏi log.
                _latency.Record(LatencyStages.LlmAnswer, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            }
        }
    }
}
