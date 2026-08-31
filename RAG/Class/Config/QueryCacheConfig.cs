namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình cache cho đường hỏi đáp. Mọi ngưỡng đều ở configuration để chỉnh mà không phải build lại.
    /// </summary>
    public class QueryCacheConfig
    {
        public const string SectionName = "QueryCache";

        /// <summary>Bật/tắt cache. Khi tắt sẽ dùng Null Object thay vì rải if trong decorator.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Trần số entry. Câu hỏi người chơi là vô hạn biến thể nên BẮT BUỘC phải chặn trần,
        /// nếu không cache sẽ phình tới hết RAM. Mỗi vector 768 chiều tốn khoảng 3KB.
        /// <para>
        /// Trần này dùng CHUNG cho cả ba loại entry (chuẩn hóa, vector, quyết định định tuyến),
        /// nên quyết định định tuyến — vốn chỉ nặng vài chục byte — vẫn chiếm chỗ ngang một
        /// vector 3KB khi cache đầy và đẩy vector ra. Nếu bật chiến lược định tuyến bằng LLM
        /// thì nên nới trần này lên.
        /// </para>
        /// </summary>
        public int MaxEntries { get; set; } = 15000;

        /// <summary>Không được dùng tới trong khoảng này thì entry bị đẩy ra.</summary>
        public int SlidingExpirationMinutes { get; set; } = 120;

        /// <summary>
        /// Thời hạn riêng, ngắn hơn, cho kết quả chuẩn hóa GIỐNG HỆT câu gốc.
        /// Bộ chuẩn hóa fail-open nên trường hợp này có thể là "câu vốn đã chuẩn" (đúng) hoặc
        /// "Gemini vừa lỗi" (sai). Thời hạn ngắn giới hạn thiệt hại của khả năng thứ hai.
        /// </summary>
        public int UnchangedNormalizationExpirationMinutes { get; set; } = 10;

        /// <summary>
        /// Thời hạn cho một quyết định định tuyến ĐÃ KHỚP route. Bảng route không đổi trong suốt
        /// vòng đời tiến trình (xem <see cref="SemanticRouterConfig"/>), nên quyết định dương
        /// không có cách nào cũ đi ngoài việc bị đẩy ra vì nguội.
        /// </summary>
        public int RouteDecisionExpirationMinutes { get; set; } = 120;

        /// <summary>
        /// Thời hạn riêng, ngắn hơn, cho quyết định "không route nào khớp".
        /// Cùng lý do với <see cref="UnchangedNormalizationExpirationMinutes"/>: router fail-open,
        /// nên kết quả này có thể là "LLM đã quyết định đi đường RAG" (đúng) hoặc "LLM vừa lỗi"
        /// (sai). Thời hạn ngắn giới hạn thiệt hại của khả năng thứ hai.
        /// </summary>
        public int NoRouteExpirationMinutes { get; set; } = 10;

        /// <summary>
        /// Nơi lưu cache xuống đĩa để nó sống qua khởi động lại và qua việc tạo lại container.
        /// Để trống thì tắt lưu đĩa, cache chỉ sống trong RAM.
        /// </summary>
        public string PersistPath { get; set; } = "App_Data/query-cache.bin";

        /// <summary>
        /// Chu kỳ ghi cache xuống đĩa. Bắt buộc phải có chứ không thể chỉ ghi lúc tắt: container
        /// thường bị dừng bằng SIGKILL, lúc đó không có shutdown êm nào chạy được.
        /// </summary>
        public int FlushIntervalSeconds { get; set; } = 300;

        /// <summary>
        /// Số entry tối đa được ghi xuống đĩa mỗi loại, chọn theo lần truy cập gần nhất.
        /// Nhỏ hơn <see cref="MaxEntries"/> của RAM vì đĩa chỉ cần giữ phần nóng nhất.
        /// Mỗi vector 768 chiều tốn khoảng 3KB, nên 2000 vector là khoảng 5,9 MB.
        /// </summary>
        public int MaxPersistedEntries { get; set; } = 2000;
    }
}
