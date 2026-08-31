namespace RAG.Interface
{
    /// <summary>
    /// Thêm câu mẫu vào một route đang chạy mà không cần khởi động lại ứng dụng.
    /// <para>
    /// Tách khỏi <see cref="ISemanticRouter"/> vì đây là đường VẬN HÀNH, không phải đường trả lời:
    /// nó nhận <c>float[]</c>, tức là mang sẵn giả định "route được nhận diện bằng vector". Giả
    /// định đó chỉ đúng với chiến lược embedding, nên để nó nằm trên hợp đồng định tuyến chung
    /// buộc mọi cài đặt khác phải mang theo một method vô nghĩa (ISP).
    /// </para>
    /// <para>
    /// Chiến lược nào không nhận diện bằng vector thì đăng ký một Null Object trả về
    /// <see cref="RouteUpdateStatus.NotSupported"/> — pipeline và controller không phải biết
    /// chiến lược nào đang chạy.
    /// </para>
    /// </summary>
    public interface IRouteUtteranceAdmin
    {
        /// <param name="routeName">Tên route đích; phải là route đã khai báo trong cấu hình.</param>
        /// <param name="utterances">Các câu dạng text; sẽ được nhúng bằng provider embedding.</param>
        /// <param name="vectors">
        /// Các vector đã chuẩn bị sẵn, nạp thẳng không qua API. Phải đúng số chiều và khác vector 0.
        /// </param>
        Task<RouteUpdateResult> AddUtterancesAsync(string routeName,
                                                   IReadOnlyList<string> utterances,
                                                   IReadOnlyList<float[]> vectors,
                                                   CancellationToken cancellationToken = default);
    }
}
