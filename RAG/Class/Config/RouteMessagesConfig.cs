namespace RAG.Class.Config
{
    /// <summary>
    /// Câu chữ trả về cho người gọi endpoint quản trị câu mẫu.
    /// <para>
    /// Nằm ở configuration vì đây là chữ người đọc, không phải logic. Bản trước sinh thẳng chuỗi
    /// tiếng Việt bên trong lớp định tuyến — tầng nghiệp vụ không nên biết người đọc là ai.
    /// </para>
    /// </summary>
    public class RouteMessagesConfig
    {
        public const string SectionName = "RouteMessages";

        /// <summary>{0} = số câu thêm được, {1} = tên route, {2} = tổng câu mẫu của route.</summary>
        public string Added { get; set; } = string.Empty;

        /// <summary>Như <see cref="Added"/> nhưng chưa ghi được xuống đĩa. Cùng bộ tham số.</summary>
        public string AddedNotPersisted { get; set; } = string.Empty;

        /// <summary>{0} = tên route đã yêu cầu, {1} = danh sách route hiện có.</summary>
        public string UnknownRoute { get; set; } = string.Empty;

        public string NotReady { get; set; } = string.Empty;

        public string NothingAdded { get; set; } = string.Empty;

        public string RouterDisabled { get; set; } = string.Empty;

        /// <summary>
        /// Chiến lược định tuyến đang chạy không hỗ trợ thêm câu mẫu lúc chạy. Khác
        /// <see cref="RouterDisabled"/>: router vẫn đang làm việc bình thường, chỉ là nó không
        /// nhận diện route bằng vector nên câu mẫu thêm vào sẽ chẳng đi tới đâu.
        /// </summary>
        public string NotSupported { get; set; } = string.Empty;

        /// <summary>Ký tự nối giữa các tên route khi liệt kê.</summary>
        public string RouteNameSeparator { get; set; } = ", ";
    }
}
