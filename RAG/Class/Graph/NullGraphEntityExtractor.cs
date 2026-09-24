using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Null Object khi <c>Graph:Enabled = false</c>. Phải có vì tầng đo độ trễ BỌC interface này vô
    /// điều kiện (nó không đọc cờ bật/tắt của đồ thị), và <c>Decorate</c> ném lúc khởi động nếu không
    /// có gì để bọc.
    /// </summary>
    public sealed class NullGraphEntityExtractor : IGraphEntityExtractor
    {
        private static readonly Task<GraphExtraction> Nothing = Task.FromResult(GraphExtraction.Empty);

        public Task<GraphExtraction> ExtractAsync(string npcName, string question, CancellationToken cancellationToken = default) =>
            Nothing;
    }
}
