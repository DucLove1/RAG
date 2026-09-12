using System.ComponentModel.DataAnnotations;
using RAG.Class.Constants;

namespace RAG.Class.Config
{
    /// <summary>
    /// Phần cấu hình DÙNG CHUNG của cache câu trả lời theo NGỮ NGHĨA, bất kể kho nào đứng sau.
    /// <para>
    /// Đây là tầng cache thứ hai và khác hẳn <see cref="QueryCacheConfig"/>: tầng kia nằm trong
    /// RAM và cache các bước TRUNG GIAN (chuẩn hóa, vector, quyết định định tuyến); tầng này cache
    /// SẢN PHẨM CUỐI. Trúng một lần ở đây là bỏ qua cả truy hồi Qdrant lẫn lượt gọi LLM sinh câu
    /// trả lời — tầng duy nhất trong pipeline cắt được lượt gọi đắt nhất.
    /// </para>
    /// <para>
    /// Khác biệt thứ hai: tầng kia khớp khi chuỗi GIỐNG HỆT, tầng này khớp theo khoảng cách cosine
    /// giữa hai vector. "Johny lúc 9h ở đâu?" và "9 giờ thì Johny ở đâu?" là hai lần trượt ở tầng
    /// kia nhưng là một lần trúng ở đây.
    /// </para>
    /// <para>
    /// Các núm riêng của từng kho nằm ở section con: <see cref="SemanticAnswerCacheRedisConfig"/>
    /// và <see cref="SemanticAnswerCacheFaissConfig"/>. Chỉ những mục Ở ĐÂY mới có nghĩa với mọi
    /// provider — đó là tiêu chí quyết định một mục thuộc về đâu.
    /// </para>
    /// </summary>
    public class SemanticAnswerCacheConfig
    {
        public const string SectionName = "SemanticAnswerCache";

        /// <summary>
        /// Bật/tắt. Mặc định TẮT. Khi tắt thì Null Object được đăng ký, nhờ vậy AskPipeline không
        /// hề biết đến cờ này. Bật ở máy dev bằng SEMANTICANSWERCACHE__ENABLED=true trong .env.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Kho nào đứng sau. Xem <see cref="AnswerCacheProvider"/> cho đánh đổi của từng lựa chọn.
        /// <para>
        /// Mặc định <c>Faiss</c> vì nó không cần gì bên ngoài: bật cache ở máy dev chỉ tốn một
        /// biến trong .env, không phải dựng container nào. Đổi sang <c>Redis</c> khi cần nhiều
        /// instance dùng CHUNG một cache — FAISS không làm được việc đó, mỗi tiến trình một cache
        /// riêng.
        /// </para>
        /// </summary>
        public AnswerCacheProvider Provider { get; set; } = AnswerCacheProvider.Faiss;

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
        /// <para>
        /// So được giữa hai provider vì cả hai đều chấm bằng cosine ĐẦY ĐỦ: Redis quy đổi khoảng
        /// cách COSINE của RediSearch thành độ tương đồng, FAISS chấm lại bằng
        /// <c>VectorMath.CosineSimilarity</c>. Đổi provider không phải chỉnh lại ngưỡng.
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
        /// Hạn dùng của một entry.
        /// <para>
        /// Với provider Redis đây là cơ chế chặn trần DUY NHẤT ở phía ứng dụng (trần cứng thứ hai
        /// đặt ở phía server bằng maxmemory + volatile-lru trong docker-compose.yml). Với FAISS thì
        /// nó đi kèm <c>SemanticAnswerCache:Faiss:MaxEntries</c>, vì trong tiến trình không có
        /// server nào chặn hộ theo dung lượng.
        /// </para>
        /// </summary>
        [Range(1, 8760)]
        public int TtlHours { get; set; } = 24;

        /// <summary>
        /// Trúng cache thì gia hạn hạn dùng, giống SlidingExpiration của MemoryQueryCache: câu hỏi
        /// nóng sống lâu, câu hỏi nguội tự rụng.
        /// <para>
        /// Ở Redis việc này chạy bằng FireAndForget nên không tốn thêm round-trip trên đường nóng,
        /// đổi lại có thể mất lệnh gia hạn (chấp nhận được: câu đó chỉ phải sinh lại một lần). Ở
        /// FAISS nó chỉ là một phép ghi Interlocked trong RAM — rẻ hơn và không mất được.
        /// </para>
        /// </summary>
        public bool SlidingTtl { get; set; } = true;
    }
}
