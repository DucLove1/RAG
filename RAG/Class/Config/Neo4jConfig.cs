using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình kết nối tới Neo4j (Aura hoặc instance tự dựng).
    /// <para>
    /// Tên section viết HOA cùng quy ước với <see cref="QDrantConfig"/>, để biến môi trường đọc ra
    /// tự nhiên: <c>NEO4J__URI</c>, <c>NEO4J__PASSWORD</c>. Mật khẩu nằm trong <c>.env</c> chứ không
    /// bao giờ nằm trong <c>appsettings.json</c>.
    /// </para>
    /// <para>
    /// Options của lớp này CHỈ được đăng ký khi <c>Graph:Enabled = true</c>. Đăng ký vô điều kiện
    /// thì <c>[Required] Password</c> cộng với <c>ValidateOnStart</c> sẽ chặn khởi động của mọi máy
    /// không có Neo4j — đúng cái bẫy đã mô tả trong <see cref="SemanticAnswerCacheRedisConfig"/>.
    /// </para>
    /// </summary>
    public class Neo4jConfig
    {
        public const string SectionName = "NEO4J";

        /// <summary>
        /// URI Bolt. Aura dùng <c>neo4j+s://xxxx.databases.neo4j.io</c> (đã bao gồm TLS và routing),
        /// instance local thường là <c>bolt://localhost:7687</c>.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Uri { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string Username { get; set; } = "neo4j";

        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Tên database. Aura Free chỉ có đúng một database tên <c>neo4j</c> và không đổi tên được.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Database { get; set; } = "neo4j";

        /// <summary>Hạn bắt tay kết nối lần đầu, tính cả DNS và TLS.</summary>
        [Range(100, 60000)]
        public int ConnectTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Trần CỨNG cho toàn bộ một lượt truy vấn nhìn từ phía ứng dụng, áp bằng
        /// <c>Task.WaitAsync</c>.
        /// <para>
        /// Phải có RIÊNG bên cạnh <see cref="TransactionTimeoutMs"/> vì hai thứ chặn hai đoạn khác
        /// nhau: timeout giao dịch chỉ bắt đầu đếm khi câu lệnh đã tới được server, nên nó không với
        /// tới DNS hỏng, bắt tay TLS treo, hay pool kết nối đã cạn. Cùng lý do với cặp
        /// OperationTimeoutMs/ServerQueryTimeoutMs của provider Redis.
        /// </para>
        /// </summary>
        [Range(100, 60000)]
        public int OperationTimeoutMs { get; set; } = 1500;

        /// <summary>Hạn PHÍA SERVER của một giao dịch, gửi kèm theo câu lệnh.</summary>
        [Range(100, 60000)]
        public int TransactionTimeoutMs { get; set; } = 1000;

        /// <summary>
        /// Trần thời gian driver tự thử lại khi gặp lỗi thoáng qua trong <c>ExecuteReadAsync</c>.
        /// Giữ thấp: đường trả lời có người chơi đang chờ, và bộ ngắt mạch mới là thứ xử lý sự cố
        /// kéo dài chứ không phải vòng retry.
        /// </summary>
        [Range(0, 60000)]
        public int MaxTransactionRetryTimeMs { get; set; } = 2000;

        [Range(1, 200)]
        public int MaxConnectionPoolSize { get; set; } = 20;

        /// <summary>
        /// Số lần hỏng liên tiếp trước khi ngắt mạch. Cùng con số với provider Redis, và cùng lý do:
        /// ba lần đủ để phân biệt một trục trặc thoáng qua với một sự cố thật.
        /// </summary>
        [Range(1, 100)]
        public int MaxConsecutiveFailures { get; set; } = 3;

        /// <summary>
        /// Mạch hở bao lâu trước khi thử lại. Trong khoảng này mọi lượt tra trả rỗng NGAY mà không
        /// chạm mạng — nếu không, một Neo4j chết sẽ thành phụ phí <see cref="ConnectTimeoutMs"/>
        /// cộng vào 100% câu hỏi.
        /// </summary>
        [Range(1, 3600)]
        public int FailureCooldownSeconds { get; set; } = 30;

        /// <summary>
        /// Mở sẵn kết nối và chạy thử câu truy vấn mở rộng một lần ngay sau khi khởi động.
        /// <para>
        /// Lượt tra ĐẦU TIÊN tới Aura phải trả cả bắt tay TLS, lấy bảng định tuyến lẫn lập kế hoạch
        /// câu Cypher — cộng lại vượt <see cref="OperationTimeoutMs"/>, nên không làm nóng thì câu
        /// hỏi đầu tiên sau mỗi lần khởi động luôn mất phần đồ thị.
        /// </para>
        /// </summary>
        public bool WarmupEnabled { get; set; } = true;

        [Range(1, 20)]
        public int WarmupMaxAttempts { get; set; } = 3;

        [Range(1, 300)]
        public int WarmupRetryDelaySeconds { get; set; } = 5;
    }
}
