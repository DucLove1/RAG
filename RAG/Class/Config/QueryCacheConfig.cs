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
        /// </summary>
        public int MaxEntries { get; set; } = 10000;

        /// <summary>Không được dùng tới trong khoảng này thì entry bị đẩy ra.</summary>
        public int SlidingExpirationMinutes { get; set; } = 120;

        /// <summary>
        /// Thời hạn riêng, ngắn hơn, cho kết quả chuẩn hóa GIỐNG HỆT câu gốc.
        /// Bộ chuẩn hóa fail-open nên trường hợp này có thể là "câu vốn đã chuẩn" (đúng) hoặc
        /// "Gemini vừa lỗi" (sai). Thời hạn ngắn giới hạn thiệt hại của khả năng thứ hai.
        /// </summary>
        public int UnchangedNormalizationExpirationMinutes { get; set; } = 10;

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
