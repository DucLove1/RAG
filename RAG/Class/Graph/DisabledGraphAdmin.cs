using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Thay cho bộ nạp thật khi đường quản trị đồ thị đang tắt hoặc đang chạy ngoài Development.
    /// <para>
    /// Chặn ở tầng ĐĂNG KÝ chứ không phải bằng một dòng <c>if</c> trong controller, cùng mẫu với
    /// <c>DisabledRouteUtteranceAdmin</c>: controller giữ nguyên hình dạng, và chính sách "khi nào
    /// được phép nạp" nằm gọn ở một chỗ trong composition root thay vì rải ra từng action.
    /// </para>
    /// <para>
    /// Ném ra 404 chứ không 403, vì 403 đã là một lời xác nhận rằng endpoint đó có tồn tại.
    /// </para>
    /// </summary>
    public sealed class DisabledGraphAdmin : IGraphLoader, IGraphSchemaAdmin
    {
        private const string Message =
            "Đường quản trị đồ thị đang tắt. Bật bằng Graph:Loader:Enabled = true và chạy ở môi " +
            "trường Development — nạp đồ thị là thao tác ghi đè dữ liệu, không để mở ở môi trường thật.";

        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) =>
            throw new GraphAdminDisabledException(Message);

        public Task<GraphLoadReport> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new GraphAdminDisabledException(Message);

        public Task<GraphVerifyReport> VerifyAsync(CancellationToken cancellationToken = default) =>
            throw new GraphAdminDisabledException(Message);
    }
}
