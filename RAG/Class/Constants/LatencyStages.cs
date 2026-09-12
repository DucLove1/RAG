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
        /// <summary>
        /// Thời gian tới TOKEN ĐẦU TIÊN, chỉ tồn tại ở đường streaming.
        /// <para>
        /// Đây là con số DUY NHẤT chứng minh streaming có tác dụng: tổng thời gian sinh chữ không
        /// đổi, thứ đổi là khoảng người chơi ngồi nhìn màn hình trống. Không đo thì không có cách
        /// nào cãi lại câu "tính năng này chẳng nhanh hơn gì".
        /// </para>
        /// <para>
        /// Tổng thời gian bơm token thì CỐ Ý dùng lại <see cref="LlmAnswer"/> chứ không đẻ tên mới,
        /// nhờ vậy dòng log của ask và của askStream so được thẳng cột với nhau cho cùng một câu
        /// hỏi, và stage này là phần cộng thêm thuần tuý.
        /// </para>
        /// </summary>
        public const string LlmFirstToken = "llmFirstToken";

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

            /// <summary>
            /// Tách khỏi <see cref="Ask"/> chứ không gộp chung. Hai phiên có tập stage khác nhau
            /// (llmFirstToken chỉ tồn tại ở đây) và tổng thời gian của phiên streaming còn bao gồm
            /// cả tốc độ ĐỌC của client. Gộp một tên thì mọi phép lọc "ask chậm" sẽ trộn hai phân
            /// bố khác hẳn nhau, và ngưỡng cảnh báo sẽ kêu ở gần như mọi request streaming.
            /// </summary>
            public const string AskStream = "askStream";
        public const string Ingest = "ingest";
    }
}
