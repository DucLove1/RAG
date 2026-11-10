using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Null Object khi <c>Graph:Enabled = false</c>. Phải có vì <c>GraphAdminController</c> luôn được
    /// MVC dựng. Không lọc gì: khi đồ thị tắt thì danh mục cũng là Null Object, nên phạm vi không
    /// còn chỗ nào để áp vào — và không có lý do gì để đọc corpus.
    /// </summary>
    public sealed class NullGraphScopeResolver : IGraphScopeResolver
    {
        public Task<GraphScope?> ResolveAsync(string npcName, string? stage, CancellationToken cancellationToken = default) =>
            Task.FromResult<GraphScope?>(GraphScope.Unrestricted(npcName));
    }
}
