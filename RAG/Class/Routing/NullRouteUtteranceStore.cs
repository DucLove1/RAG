using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Null Object cho <see cref="IRouteUtteranceStore"/>: không nạp và không lưu gì.
    /// Được đăng ký khi SemanticRouter:UtteranceStorePath để trống. Câu mẫu thêm lúc chạy vẫn có hiệu
    /// lực ngay trong bộ nhớ, nhưng <see cref="SaveAsync"/> trả về false để endpoint nói rõ với người
    /// gọi rằng thay đổi sẽ mất khi khởi động lại.
    /// </summary>
    public sealed class NullRouteUtteranceStore : IRouteUtteranceStore
    {
        public Task<IReadOnlyList<StoredUtterance>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoredUtterance>>(Array.Empty<StoredUtterance>());

        public Task<bool> SaveAsync(IReadOnlyList<StoredUtterance> utterances, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
