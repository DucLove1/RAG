using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Phần cấu hình chỉ có nghĩa với provider <c>Redis</c> của tầng cache ngữ nghĩa.
    /// <para>
    /// Tách khỏi <see cref="SemanticAnswerCacheConfig"/> chứ không nhồi chung, và đó là điều kiện
    /// để chạy được FAISS: <c>AddValidatedOptions</c> gắn <c>ValidateOnStart</c>, mà
    /// <c>ValidateOnStart</c> CHỈ chạy cho những options type đã được ĐĂNG KÝ. Nằm riêng thì nhánh
    /// FAISS đơn giản là không đăng ký class này, nên <see cref="ConnectionString"/> mang
    /// <c>[Required]</c> không bao giờ bị đánh giá — thay vì phải gỡ <c>[Required]</c> và mất luôn
    /// việc kiểm tra ở chính nhánh cần nó.
    /// </para>
    /// <para>
    /// Cố ý KHÔNG lồng làm property của class cha (kiểu <c>SemanticRouterConfig.Embedding</c>):
    /// <c>ValidateDataAnnotations</c> KHÔNG đệ quy vào property phức hợp, nên lồng vào là mọi
    /// <c>[Range]</c>/<c>[Required]</c> dưới đây im lặng không chạy — đổi một vấn đề thật lấy một
    /// vấn đề tệ hơn vì nó vô hình.
    /// </para>
    /// </summary>
    public class SemanticAnswerCacheRedisConfig
    {
        /// <summary>
        /// Dẫn xuất từ section cha chứ không viết rời. Đổi tên section cha mà quên đổi con thì
        /// bind hụt về mặc định, và triệu chứng duy nhất là "cache im lặng không hoạt động".
        /// </summary>
        public const string SectionName = SemanticAnswerCacheConfig.SectionName + ":Redis";

        /// <summary>
        /// Chuỗi kết nối theo cú pháp StackExchange.Redis. Không phải bí mật, nhưng KHÁC NHAU giữa
        /// các môi trường nên chỗ đúng của nó là biến môi trường: máy dev là "localhost:6379",
        /// dịch vụ quản lý là "host:port,ssl=true,password=...".
        /// <para>
        /// ⚠️ Biến môi trường là <c>SemanticAnswerCache__Redis__ConnectionString</c> (HAI dấu gạch
        /// dưới ở giữa). Tên phẳng cũ <c>SemanticAnswerCache__ConnectionString</c> nay bind vào hư
        /// không — app sẽ từ chối khởi động nếu còn đặt nó, xem <c>ObsoleteAnswerCacheKeys</c>.
        /// </para>
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
        /// <para>
        /// Provider FAISS KHÔNG có cần gạt tương đương, và đó là điểm nó tốt hơn: vân tay của file
        /// cache ở đó đã mang sẵn model + số chiều nên đổi model là TỰ ĐỘNG vô hiệu hoá cache.
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
