using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình cache câu trả lời theo NGỮ NGHĨA (Redis + RediSearch).
    /// <para>
    /// Đây là tầng cache thứ hai và khác hẳn <see cref="QueryCacheConfig"/>: tầng kia nằm trong
    /// RAM và cache các bước TRUNG GIAN (chuẩn hóa, vector, quyết định định tuyến); tầng này nằm
    /// ngoài tiến trình và cache SẢN PHẨM CUỐI. Trúng một lần ở đây là bỏ qua cả truy hồi Qdrant
    /// lẫn lượt gọi LLM sinh câu trả lời — tầng duy nhất trong pipeline cắt được lượt gọi đắt nhất.
    /// </para>
    /// <para>
    /// Khác biệt thứ hai: tầng kia khớp khi chuỗi GIỐNG HỆT, tầng này khớp theo khoảng cách cosine
    /// giữa hai vector. "Johny lúc 9h ở đâu?" và "9 giờ thì Johny ở đâu?" là hai lần trượt ở tầng
    /// kia nhưng là một lần trúng ở đây.
    /// </para>
    /// </summary>
    public class SemanticAnswerCacheConfig
    {
        public const string SectionName = "SemanticAnswerCache";

        /// <summary>
        /// Bật/tắt. Mặc định TẮT, và đó là chủ ý: Redis là một tiến trình bên ngoài mà không phải
        /// môi trường nào cũng có. Bật ở máy dev bằng SEMANTICANSWERCACHE__ENABLED=true trong .env.
        /// Khi tắt thì Null Object được đăng ký, nhờ vậy AskPipeline không hề biết đến cờ này.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Chuỗi kết nối theo cú pháp StackExchange.Redis. Không phải bí mật, nhưng KHÁC NHAU giữa
        /// các môi trường nên chỗ đúng của nó là biến môi trường: máy dev là "localhost:6379",
        /// dịch vụ quản lý là "host:port,ssl=true,password=...".
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string ConnectionString { get; set; } = "localhost:6379";

        [Range(0, 15)]
        public int Database { get; set; } = 0;

        /// <summary>
        /// Tên index RediSearch, ĐỒNG THỜI là cần gạt đổi phiên bản cache.
        /// <para>
        /// ⚠️ ĐỔI MODEL NHÚNG HAY NẠP LẠI QDRANT THÌ PHẢI BUMP HẬU TỐ Ở ĐÂY (v1 -&gt; v2).
        /// Khóa cache cố ý không mang tên model, nên không bump nghĩa là cache tiếp tục trả câu
        /// trả lời dựng trên dữ liệu cũ mà KHÔNG có một dòng lỗi nào — cùng loại triệu chứng im
        /// lặng đã khiến <see cref="QDrantConfig"/> phải bỏ hẳn trường Dimensions.
        /// Dọn index cũ: FT.DROPINDEX &lt;tên cũ&gt; DD (DD để xoá luôn document, không thì còn lại
        /// một đống hash 3KB không ai trỏ tới).
        /// </para>
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string IndexName { get; set; } = "idx:npc-answers-v1";

        /// <summary>
        /// Tiền tố khóa document. DẪN XUẤT từ <see cref="IndexName"/>, cố ý KHÔNG phải một mục
        /// cấu hình riêng.
        /// <para>
        /// PREFIX của FT.CREATE và tiền tố lúc HSET bắt buộc phải khớp nhau. Tách thành hai mục
        /// cấu hình thì người bump IndexName sang v2 mà quên bump tiền tố sẽ tạo ra một index v2
        /// HÚT SẠCH document v1 — vector của model cũ nằm trong index của model mới, và triệu
        /// chứng duy nhất là cache trả về những câu trả lời sai một cách khó hiểu. Dẫn xuất thì
        /// lỗi đó không xảy ra được, thay vì phải trông cậy vào việc nhớ.
        /// </para>
        /// </summary>
        public string KeyPrefix => $"{IndexName}:doc:";

        /// <summary>
        /// Ngưỡng độ tương đồng cosine để coi là trúng.
        /// <para>
        /// Đặt cao hơn hẳn ngưỡng 0.78-0.80 của node định tuyến, và có lý do: định tuyến đoán sai
        /// chỉ làm NPC đáp lệch giọng, còn ở đây đoán sai là ĐƯA BẰNG CHỨNG SAI cho người chơi
        /// trong một game trinh thám — họ dựng cả chuỗi suy luận lên đó. Hai kiểu va chạm nằm
        /// đúng quanh 0.95: lật phủ định ("Johny CÓ ở nhà lúc 9h không" / "Johny KHÔNG ở nhà lúc
        /// 9h phải không" — embedding nổi tiếng yếu với phủ định) và đổi tên riêng trong câu dài
        /// ("Chris chết lúc mấy giờ" / "Danie chết lúc mấy giờ").
        /// </para>
        /// <para>
        /// Bất đối xứng rất mạnh nên nghiêng hẳn về phía cao: trúng nhầm là hỏng cả buổi chơi,
        /// trượt nhầm chỉ tốn một lượt gọi LLM. Cách chỉnh: chơi thử một buổi, đọc các dòng log
        /// "Trúng cache ngữ nghĩa" (in ra CẢ HAI câu), rồi hạ dần cho tới khi bắt đầu thấy cặp sai
        /// nghĩa và lùi lại một nấc. Đừng đoán trước.
        /// </para>
        /// </summary>
        [Range(0.0, 1.0)]
        public double SimilarityThreshold { get; set; } = 0.97;

        /// <summary>
        /// Câu ngắn hơn ngần này ký tự thì không ghi cache. Câu cực ngắn ("ừ", "rồi sao", "thế à")
        /// nhúng ra vector nhiễu, gần như thứ gì cũng vượt ngưỡng — đúng chỗ va chạm nhầm tập trung.
        /// </summary>
        [Range(0, 500)]
        public int MinCacheableQuestionLength { get; set; } = 8;

        /// <summary>
        /// Có ghi cache câu trả lời sinh ra từ ngữ cảnh RỖNG hay không. Mặc định KHÔNG: truy hồi
        /// không ra gì thì LLM gần như chắc chắn trả "tôi không biết", và ghi lại nghĩa là đóng
        /// băng một lần Qdrant hụt thành câu trả lời chính thức cho cả một chùm câu hỏi cho tới
        /// khi entry hết hạn.
        /// </summary>
        public bool CacheAnswersWithoutContext { get; set; } = false;

        /// <summary>
        /// Hạn dùng của một entry. Đây là cơ chế chặn trần DUY NHẤT ở phía ứng dụng — Redis không
        /// có thứ tương đương MaxEntries của MemoryCache. Trần cứng thứ hai đặt ở phía server bằng
        /// maxmemory + volatile-lru trong docker-compose.yml.
        /// </summary>
        [Range(1, 8760)]
        public int TtlHours { get; set; } = 24;

        /// <summary>
        /// Trúng cache thì gia hạn hạn dùng, giống SlidingExpiration của MemoryQueryCache: câu hỏi
        /// nóng sống lâu, câu hỏi nguội tự rụng. Chạy bằng FireAndForget nên không tốn thêm
        /// round-trip trên đường nóng; mất lệnh gia hạn chỉ khiến câu đó phải sinh lại một lần,
        /// nên không đáng để chờ.
        /// </summary>
        public bool SlidingTtl { get; set; } = true;

        /// <summary>
        /// Dùng chỉ mục FLAT (quét tuyến tính) thay cho HNSW. Mặc định BẬT.
        /// <para>
        /// Ở quy mô vài chục nghìn entry, quét tuyến tính 768 chiều đã dưới mili-giây mà lại cho
        /// recall CHÍNH XÁC. HNSW để lại tombstone khi xoá và chỉ thu hồi bộ nhớ qua GC nền —
        /// trong khi cache thì theo định nghĩa là churn cao. Tệ hơn: HNSW đi cùng bộ lọc TAG rơi
        /// vào đường truy vấn lai, recall tụt không đoán trước được, và một lần trượt nhầm ở đó
        /// hoàn toàn vô hình mà vẫn tốn một lượt gọi LLM.
        /// Tắt (chuyển sang HNSW) khi FT.INFO báo num_docs lên tới hàng trăm nghìn.
        /// </para>
        /// </summary>
        public bool UseFlatIndex { get; set; } = true;

        /// <summary>Chỉ có tác dụng khi <see cref="UseFlatIndex"/> = false.</summary>
        [Range(2, 512)]
        public int HnswM { get; set; } = 16;

        /// <summary>Chỉ có tác dụng khi <see cref="UseFlatIndex"/> = false.</summary>
        [Range(4, 4096)]
        public int HnswEfConstruction { get; set; } = 200;

        /// <summary>Hạn kết nối lần đầu. Chỉ phải trả một lần nhờ bộ ngắt mạch bên dưới.</summary>
        [Range(100, 30000)]
        public int ConnectTimeoutMs { get; set; } = 2000;

        [Range(0, 10)]
        public int ConnectRetry { get; set; } = 3;

        /// <summary>
        /// Hạn cho MỘT thao tác cache. Nguyên tắc chọn: phải đủ nhỏ để "trả giá rồi vẫn trượt" còn
        /// rẻ hơn thứ mà cache thay thế. KNN FLAT trên vài nghìn vector 768 chiều là khoảng 1-3ms
        /// ở p99, nên 250ms đã dư khoảng 100 lần mà vẫn thấp hơn một lượt gọi LLM cả chục lần.
        /// </summary>
        [Range(20, 5000)]
        public int OperationTimeoutMs { get; set; } = 250;

        /// <summary>
        /// Đối số TIMEOUT của chính lệnh FT.SEARCH, để RediSearch tự bỏ cuộc. Đặt NHỎ HƠN
        /// <see cref="OperationTimeoutMs"/> để server chủ động trả kết quả rỗng (chính sách mặc
        /// định ON_TIMEOUT là RETURN, và rỗng nghĩa là trượt, tức là fail-open) thay vì để client
        /// cắt dây — bỏ cuộc phía server thì lệnh không còn treo lại trên kết nối.
        /// </summary>
        [Range(10, 5000)]
        public int ServerQueryTimeoutMs { get; set; } = 100;

        /// <summary>
        /// Số lần lỗi liên tiếp trước khi ngắt mạch.
        /// <para>
        /// Không có bộ ngắt này thì Redis chết sẽ khiến MỌI request trả trọn
        /// <see cref="ConnectTimeoutMs"/> trước khi rơi xuống đường thường — tức là cache làm app
        /// CHẬM HƠN so với khi không có cache, đúng thứ nó sinh ra để tránh.
        /// </para>
        /// </summary>
        [Range(1, 100)]
        public int MaxConsecutiveFailures { get; set; } = 3;

        /// <summary>Ngắt mạch rồi thì nghỉ bấy nhiêu giây mới cho một request thăm dò thử lại.</summary>
        [Range(1, 3600)]
        public int FailureCooldownSeconds { get; set; } = 30;
    }
}
