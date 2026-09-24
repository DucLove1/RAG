using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Null Object khi <c>Graph:Enabled = false</c>. Phải có vì <c>GraphAdminController</c> luôn được
    /// MVC dựng: thiếu đăng ký thì mọi endpoint quản trị đồ thị chết bằng 500 thay vì trả rỗng.
    /// </summary>
    public sealed class NullGraphEntityCatalog : IGraphEntityCatalog
    {
        private static readonly Task<NpcEntityCatalog> Nothing = Task.FromResult(NpcEntityCatalog.Empty);

        public Task<NpcEntityCatalog> GetAsync(string npcName, CancellationToken cancellationToken = default) =>
            Nothing;
    }
}
