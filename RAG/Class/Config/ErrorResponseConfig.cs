namespace RAG.Class.Config
{
    /// <summary>
    /// Tiêu đề cho các phản hồi lỗi. Đưa ra configuration vì đây là chữ người dùng cuối đọc được:
    /// đổi cách diễn đạt, hay dịch sang ngôn ngữ khác, không nên phải build lại ứng dụng.
    /// </summary>
    public class ErrorResponseConfig
    {
        public const string SectionName = "ErrorResponses";

        /// <summary>Nhà cung cấp embedding đang giới hạn tần suất (429).</summary>
        public string RateLimitedTitle { get; set; } = string.Empty;

        /// <summary>Nhà cung cấp embedding lỗi hoặc không phản hồi được (503).</summary>
        public string EmbeddingUnavailableTitle { get; set; } = string.Empty;

        /// <summary>Dữ liệu đồ thị viết tay không hợp lệ (400).</summary>
        public string GraphDataInvalidTitle { get; set; } = string.Empty;

        /// <summary>Thao tác quản trị đồ thị đang tắt (404).</summary>
        public string GraphAdminDisabledTitle { get; set; } = string.Empty;

        /// <summary>Không nói chuyện được với Neo4j trên đường quản trị (503).</summary>
        public string GraphUnavailableTitle { get; set; } = string.Empty;

        /// <summary>Neo4j từ chối câu lệnh quản trị — xung đột schema, sai cú pháp (400).</summary>
        public string GraphStatementRejectedTitle { get; set; } = string.Empty;

        /// <summary>Mọi lỗi ngoài dự kiến khác (500).</summary>
        public string UnexpectedTitle { get; set; } = string.Empty;
    }
}
