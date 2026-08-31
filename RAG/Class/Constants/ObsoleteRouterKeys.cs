namespace RAG.Class.Constants
{
    /// <summary>
    /// Bẫy di trú cấu hình: các khóa của node định tuyến đã dời chỗ khi router tách thành hai
    /// chiến lược. Khóa cũ giờ bind vào hư không.
    /// <para>
    /// Phải nổ lúc khởi động chứ không thể chỉ ghi log, vì triệu chứng khi bỏ sót là hoàn toàn
    /// im lặng: <c>VectorCachePath</c> và <c>UtteranceStorePath</c> đang được đặt bằng biến môi
    /// trường trong Dockerfile và trên dashboard Render để trỏ vào Disk gắn ngoài. Bind hụt thì
    /// app quay về đường mặc định tương đối trong container — vẫn chạy, vẫn trả lời đúng, chỉ là
    /// MỖI LẦN DEPLOY nhúng lại toàn bộ câu mẫu (~2-3 phút, đốt hạn mức 100 request/phút của
    /// Gemini) và không có dòng lỗi nào ở bất cứ đâu để lần ra.
    /// </para>
    /// <para>
    /// Câu chữ nằm ở đây chứ không ở configuration là có chủ ý: đây là thông báo cho người vận
    /// hành đọc lúc app từ chối khởi động, và nó phải đọc được kể cả khi file cấu hình sai.
    /// </para>
    /// </summary>
    public static class ObsoleteRouterKeys
    {
        /// <summary>Khóa cũ (tương đối trong section SemanticRouter) → chỗ ở mới.</summary>
        public static readonly IReadOnlyDictionary<string, string> Moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Enabled"] = "SemanticRouter:Strategy (Off | Embedding | Llm)",
            ["SimilarityThreshold"] = "SemanticRouter:Embedding:SimilarityThreshold",
            ["MaxRoutableLength"] = "SemanticRouter:Embedding:MaxRoutableLength",
            ["VectorCachePath"] = "SemanticRouter:Embedding:VectorCachePath",
            ["UtteranceStorePath"] = "SemanticRouter:Embedding:UtteranceStorePath",
            ["WarmupRetryDelaySeconds"] = "SemanticRouter:Embedding:WarmupRetryDelaySeconds",
            ["WarmupMaxAttempts"] = "SemanticRouter:Embedding:WarmupMaxAttempts"
        };

        public const string MessageHeader =
            "Cấu hình node định tuyến đã đổi bố cục: các núm riêng của từng chiến lược nay nằm trong " +
            "section con. Những khóa sau vẫn đang được đặt (appsettings.json hoặc biến môi trường " +
            "dạng SemanticRouter__<Tên>) nhưng không còn được đọc — hãy đổi tên chúng:";

        public const string MessageFooter =
            "Nếu đang deploy bằng Docker/Render: đổi luôn tên biến môi trường tương ứng " +
            "(ví dụ SemanticRouter__VectorCachePath thành SemanticRouter__Embedding__VectorCachePath), " +
            "nếu không cache vector sẽ rơi ra ngoài Disk gắn ngoài và bị nhúng lại sau mỗi lần deploy.";

        public const string MessageLineFormat = "  - {0}  ->  {1}";
    }
}
