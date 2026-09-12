using RAG.Class.Config;

namespace RAG.Class.Constants
{
    /// <summary>
    /// Bẫy di trú cấu hình: các khóa của cache ngữ nghĩa đã dời chỗ khi tầng này tách thành hai
    /// provider. Khóa cũ giờ bind vào hư không.
    /// <para>
    /// Phải nổ lúc khởi động chứ không thể chỉ ghi log, vì triệu chứng khi bỏ sót là hoàn toàn im
    /// lặng và còn tệ hơn trường hợp của <see cref="ObsoleteRouterKeys"/>:
    /// <c>SemanticAnswerCache__ConnectionString</c> đang được đặt trong .env và trên dashboard
    /// Render. Bind hụt thì nó rơi về mặc định <c>localhost:6379</c> — chỗ không có Redis nào —
    /// và luật fail-open của chính tầng này NUỐT SẠCH lỗi kết nối. Kết quả: cache chết hoàn toàn,
    /// mọi request vẫn trả lời đúng, vẫn trả trọn tiền LLM, và không có một dòng lỗi nào. Chỉ có
    /// con số <c>errors</c> ở cache-stats là nhúc nhích, mà không ai nhìn nó hàng ngày.
    /// </para>
    /// <para>
    /// Câu chữ nằm ở đây chứ không ở configuration là có chủ ý: đây là thông báo cho người vận
    /// hành đọc lúc app từ chối khởi động, và nó phải đọc được kể cả khi file cấu hình sai.
    /// </para>
    /// </summary>
    public static class ObsoleteAnswerCacheKeys
    {
        /// <summary>Khóa cũ (tương đối trong section SemanticAnswerCache) → chỗ ở mới.</summary>
        public static readonly IReadOnlyDictionary<string, string> Moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionString"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:ConnectionString",
            ["Database"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:Database",
            ["IndexName"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:IndexName",
            ["UseFlatIndex"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:UseFlatIndex",
            ["HnswM"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:HnswM",
            ["HnswEfConstruction"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:HnswEfConstruction",
            ["ConnectTimeoutMs"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:ConnectTimeoutMs",
            ["ConnectRetry"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:ConnectRetry",
            ["OperationTimeoutMs"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:OperationTimeoutMs",
            ["ServerQueryTimeoutMs"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:ServerQueryTimeoutMs",
            ["MaxConsecutiveFailures"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:MaxConsecutiveFailures",
            ["FailureCooldownSeconds"] = $"{SemanticAnswerCacheRedisConfig.SectionName}:FailureCooldownSeconds"
        };

        public const string MessageHeader =
            "Cấu hình cache ngữ nghĩa đã đổi bố cục: tầng này nay có hai provider (Redis | Faiss) nên các " +
            "núm riêng của từng kho đã dời vào section con. Những khóa sau vẫn đang được đặt " +
            "(appsettings.json hoặc biến môi trường dạng SemanticAnswerCache__<Tên>) nhưng không còn được " +
            "đọc — hãy đổi tên chúng:";

        public const string MessageFooter =
            "Nếu đang deploy bằng Docker/Render: đổi luôn tên biến môi trường tương ứng " +
            "(SemanticAnswerCache__ConnectionString thành SemanticAnswerCache__Redis__ConnectionString). " +
            "Bỏ sót thì chuỗi kết nối rơi về localhost:6379, fail-open nuốt sạch lỗi, và cache chết " +
            "hoàn toàn mà không có dòng lỗi nào.";

        public const string MessageLineFormat = "  - {0}  ->  {1}";
    }
}
