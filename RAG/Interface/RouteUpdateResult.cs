namespace RAG.Interface
{
    /// <summary>
    /// Kết cục của một lần thêm câu mẫu lúc chạy.
    /// </summary>
    public enum RouteUpdateStatus
    {
        /// <summary>Đã thêm được ít nhất một câu mẫu.</summary>
        Added = 0,

        /// <summary>Không có route nào mang tên đã yêu cầu.</summary>
        UnknownRoute = 1,

        /// <summary>Router chưa nạp xong vector nên chưa thể dựng lại bảng route.</summary>
        NotReady = 2,

        /// <summary>Không câu nào được thêm: trùng lặp hoặc vector không hợp lệ.</summary>
        NothingAdded = 3,

        /// <summary>Node định tuyến đang tắt.</summary>
        RouterDisabled = 4
    }

    /// <summary>
    /// Kết quả của một lần thêm câu mẫu lúc chạy.
    /// <para>
    /// Mang MÃ trạng thái và số liệu, KHÔNG mang câu chữ hiển thị. Bản trước sinh thẳng chuỗi
    /// tiếng Việt trong lớp nghiệp vụ — nghĩa là muốn sửa cách diễn đạt hay dịch sang ngôn ngữ
    /// khác thì phải build lại, và tầng nghiệp vụ phải biết người đọc là ai.
    /// </para>
    /// </summary>
    /// <param name="Status">Kết cục của lần thêm.</param>
    /// <param name="RouteName">Tên route đích, đúng như đã khai trong cấu hình.</param>
    /// <param name="Added">Số câu mẫu thực sự được thêm.</param>
    /// <param name="Skipped">Số câu bị bỏ vì trùng lặp hoặc vector không hợp lệ.</param>
    /// <param name="TotalInRoute">Tổng số câu mẫu của route sau khi thêm.</param>
    /// <param name="Persisted">
    /// Thay đổi đã được ghi xuống đĩa hay chưa. Nếu <c>false</c>, route vẫn hoạt động ngay
    /// nhưng sẽ mất khi khởi động lại.
    /// </param>
    /// <param name="KnownRoutes">Các route đang có; chỉ điền khi <see cref="RouteUpdateStatus.UnknownRoute"/>.</param>
    public sealed record RouteUpdateResult(
        RouteUpdateStatus Status,
        string RouteName,
        int Added,
        int Skipped,
        int TotalInRoute,
        bool Persisted,
        IReadOnlyList<string> KnownRoutes)
    {
        public bool Success => Status == RouteUpdateStatus.Added;

        public static RouteUpdateResult Succeeded(string routeName, int added, int skipped, int totalInRoute, bool persisted) =>
            new(RouteUpdateStatus.Added, routeName, added, skipped, totalInRoute, persisted, Array.Empty<string>());

        public static RouteUpdateResult UnknownRoute(string routeName, IReadOnlyList<string> knownRoutes) =>
            new(RouteUpdateStatus.UnknownRoute, routeName, 0, 0, 0, false, knownRoutes);

        public static RouteUpdateResult NotReady() =>
            new(RouteUpdateStatus.NotReady, string.Empty, 0, 0, 0, false, Array.Empty<string>());

        public static RouteUpdateResult NothingAdded(int skipped, int totalInRoute) =>
            new(RouteUpdateStatus.NothingAdded, string.Empty, 0, skipped, totalInRoute, false, Array.Empty<string>());

        public static RouteUpdateResult RouterDisabled() =>
            new(RouteUpdateStatus.RouterDisabled, string.Empty, 0, 0, 0, false, Array.Empty<string>());
    }
}
