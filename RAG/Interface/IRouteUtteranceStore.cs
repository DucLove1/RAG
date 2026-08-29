namespace RAG.Interface
{
    /// <summary>
    /// Một câu mẫu được thêm lúc chạy qua endpoint, kèm sẵn vector của nó.
    /// Tự mang vector nên khi khởi động lại không phải gọi API để nhúng lại.
    /// </summary>
    /// <param name="Route">Tên route mà câu mẫu này thuộc về.</param>
    /// <param name="Text">Câu mẫu, hoặc nhãn mô tả nếu người dùng nạp thẳng vector.</param>
    /// <param name="Vector">Vector đã chuẩn bị sẵn.</param>
    public sealed record StoredUtterance(string Route, string Text, float[] Vector);

    /// <summary>
    /// Kho câu mẫu bổ sung lúc chạy, tách khỏi appsettings.json.
    /// <para>
    /// Cố tình KHÔNG ghi ngược vào appsettings.json: file cấu hình là thứ con người viết và đưa vào
    /// version control, ứng dụng không nên tự sửa nó. Câu mẫu thêm lúc chạy là dữ liệu vận hành,
    /// nên có vòng đời riêng.
    /// </para>
    /// </summary>
    public interface IRouteUtteranceStore
    {
        /// <summary>Nạp toàn bộ câu mẫu đã thêm. Lỗi đọc được nuốt và trả về danh sách rỗng.</summary>
        Task<IReadOnlyList<StoredUtterance>> LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Ghi lại toàn bộ danh sách (ghi đè). Trả về <c>false</c> khi không lưu được,
        /// để caller báo cho người gọi biết thay đổi chỉ tồn tại tới lần khởi động lại.
        /// </summary>
        Task<bool> SaveAsync(IReadOnlyList<StoredUtterance> utterances, CancellationToken cancellationToken = default);
    }
}
