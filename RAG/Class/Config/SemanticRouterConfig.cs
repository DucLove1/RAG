namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình node định tuyến ngữ nghĩa. Route chỉ là bộ lọc "trả lời thẳng":
    /// KHÔNG khai báo route cho RAG, vì RAG là đường mặc định khi không route nào khớp.
    /// <para>
    /// Cố tình bind qua <c>IOptions</c> chứ không phải <c>IOptionsMonitor</c>: router là singleton giữ
    /// vector dẫn xuất từ <see cref="Routes"/>, nên reload-on-change sẽ âm thầm làm cấu hình lệch khỏi
    /// cache (câu mẫu mới không có vector). Đổi câu mẫu đòi hỏi khởi động lại ứng dụng.
    /// </para>
    /// </summary>
    public class SemanticRouterConfig
    {
        public const string SectionName = "SemanticRouter";

        /// <summary>Bật/tắt node. Khi tắt, pipeline dùng bản cài đặt passthrough.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Ngưỡng cosine mặc định; route nào không tự khai ngưỡng thì dùng giá trị này.</summary>
        public double SimilarityThreshold { get; set; } = 0.80;

        /// <summary>
        /// Câu dài hơn ngưỡng này thì bỏ qua việc chấm điểm và đi thẳng đường RAG.
        /// Đây là phòng thủ rẻ nhất cho câu pha trộn ý định ("chào bạn, cho tôi hỏi giá kiếm sắt"):
        /// câu tán gẫu thật gần như luôn ngắn.
        /// </summary>
        public int MaxRoutableLength { get; set; } = 60;

        /// <summary>
        /// Nơi lưu vector câu mẫu để lần khởi động sau không phải gọi lại API embedding.
        /// Để trống thì tắt cache (luôn nhúng lại mỗi lần khởi động).
        /// </summary>
        public string VectorCachePath { get; set; } = "App_Data/route-vectors.json";

        /// <summary>
        /// Nơi lưu câu mẫu được thêm lúc chạy qua endpoint, tách khỏi appsettings.json.
        /// Để trống thì câu mẫu thêm vào chỉ sống trong bộ nhớ tới lần khởi động lại.
        /// </summary>
        public string UtteranceStorePath { get; set; } = "App_Data/route-utterances.json";

        /// <summary>Khoảng nghỉ giữa hai lần thử nạp vector khi warm-up thất bại.</summary>
        public int WarmupRetryDelaySeconds { get; set; } = 30;

        /// <summary>Số lần thử nạp vector tối đa trước khi từ bỏ tới lần khởi động sau.</summary>
        public int WarmupMaxAttempts { get; set; } = 5;

        public List<SemanticRouteConfig> Routes { get; set; } = new();
    }

    /// <summary>
    /// Một route "trả lời thẳng": khớp thì bỏ qua truy hồi Qdrant và để LLM sinh câu trả lời
    /// theo persona NPC nhưng không kèm ngữ cảnh nào.
    /// </summary>
    public class SemanticRouteConfig
    {
        /// <summary>Tên route, chỉ dùng để log và chẩn đoán.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Các câu mẫu đại diện cho route; được nhúng một lần rồi giữ trong RAM.</summary>
        public List<string> Utterances { get; set; } = new();

        /// <summary>Ngưỡng riêng của route; bỏ trống thì lấy ngưỡng mặc định toàn cục.</summary>
        public double? SimilarityThreshold { get; set; }

        /// <summary>
        /// System prompt riêng cho câu trả lời không có ngữ cảnh; {0} = tên NPC, {1} = tính cách NPC.
        /// Bỏ trống thì dùng Prompts:AnswerSystemTemplate — nhưng lưu ý template mặc định có câu
        /// "nếu không có ngữ cảnh thì trả lời không biết", vốn phản tác dụng trên nhánh này.
        /// Dấu ngoặc nhọn literal phải escape thành {{ }} vì template đi qua string.Format.
        /// </summary>
        public string SystemPromptTemplate { get; set; } = string.Empty;

        /// <summary>Template user prompt cho câu trả lời không truy hồi; {0} = câu hỏi đã chuẩn hóa.</summary>
        public string UserPromptTemplate { get; set; } = string.Empty;
    }
}
