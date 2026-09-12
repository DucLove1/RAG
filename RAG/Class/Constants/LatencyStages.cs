namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên các stage được đo. Hằng chứ không phải chuỗi rải rác: tên stage xuất hiện ở hai nơi cách
    /// xa nhau — decorator ghi vào, và cấu hình <c>Latency:DisabledStages</c> lọc ra. Gõ lệch một
    /// chữ ở chỗ thứ hai thì cấu hình lặng lẽ không có tác dụng gì.
    /// </summary>
    public static class LatencyStages
    {
        // Đường trả lời, theo đúng thứ tự AskPipeline chạy.
        public const string Normalize = "normalize";
        public const string Route = "route";
        public const string WeakPoint = "weakPoint";
        public const string Embedding = "embedding";
        public const string AnswerCacheGet = "answerCacheGet";
        public const string VectorSearch = "vectorSearch";
        public const string LlmAnswer = "llmAnswer";
        public const string AnswerCacheSet = "answerCacheSet";

        // Đường nạp dữ liệu.
        public const string EmbeddingBatch = "embeddingBatch";
        public const string VectorUpsert = "vectorUpsert";
        public const string CollectionEnsure = "collectionEnsure";
        public const string CollectionCreate = "collectionCreate";
    }

    /// <summary>Tên các phiên đo, tức giá trị <c>Operation</c> của báo cáo.</summary>
    public static class LatencyOperations
    {
        public const string Ask = "ask";
        public const string Ingest = "ingest";
    }
}
