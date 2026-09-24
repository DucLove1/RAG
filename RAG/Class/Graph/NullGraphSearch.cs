using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Null Object cho tầng đồ thị: luôn trả ngữ cảnh rỗng, không chạm Neo4j, không cần một biến
    /// môi trường nào. Được đăng ký khi <c>Graph:Enabled = false</c>, nhờ đó tầng dựng ngữ cảnh
    /// không cần một dòng <c>if</c> nào cho cờ bật/tắt — cùng vai trò với
    /// <see cref="Caching.NullSemanticAnswerCache"/>.
    /// <para>
    /// Trả <see cref="GraphContext.Empty"/> chứ KHÔNG phải <c>DegradedEmpty</c>, và khác biệt đó có
    /// hệ quả thật: tắt đồ thị là một lựa chọn, không phải một sự cố, nên câu trả lời sinh ra lúc
    /// tắt vẫn được phép ghi vào cache.
    /// </para>
    /// </summary>
    public sealed class NullGraphSearch : IGraphSearch, IGraphStatistics
    {
        private static readonly Task<GraphContext> Nothing = Task.FromResult(GraphContext.Empty);

        public Task<GraphContext> SearchAsync(GraphSearchQuery query, CancellationToken cancellationToken = default) =>
            Nothing;

        /// <summary>Toàn số 0 chứ không ném: endpoint số liệu phải trả lời được cả khi đồ thị đang tắt.</summary>
        public GraphStats GetStats() => new(0, 0, 0);
    }
}
