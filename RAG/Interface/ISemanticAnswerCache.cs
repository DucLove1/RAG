namespace RAG.Interface
{
    /// <summary>
    /// Mọi thứ định danh MỘT lượt tra cache ngữ nghĩa. Gói vào một record thay vì bốn tham số rời
    /// để đường đọc và đường ghi không thể lệch nhau về cách phân vùng — đó đúng là loại lỗi chỉ
    /// lộ ra khi một NPC trả lời bằng câu của NPC khác.
    /// </summary>
    /// <param name="NpcPersona">
    /// Mô tả tính cách NPC, tức <c>NpcSystem</c> của request. PHẢI nằm trong phân vùng: nó do
    /// CLIENT gửi lên theo từng request và đi thẳng vào system prompt, nên cùng một tên NPC với
    /// hai persona khác nhau là hai câu trả lời khác nhau. Phân vùng chỉ theo tên NPC sẽ trả câu
    /// trả lời sinh theo persona này cho một request khai persona kia.
    /// </param>
    public sealed record SemanticAnswerQuery(string NpcName,
                                             string NpcPersona,
                                             string Question,
                                             float[] QuestionVector);

    /// <param name="CachedQuestion">
    /// Câu hỏi đã được lưu trước đó, tức là câu mà lần này khớp vào. Có mặt để ghi log: in mỗi
    /// độ tương đồng thì vô dụng, vì 0,964 tự nó không nói lên nó khớp cái gì.
    /// </param>
    /// <param name="Similarity">
    /// Độ tương đồng thực tế của lần khớp. Không có nó thì ngưỡng chỉ có thể chỉnh bằng cách đoán.
    /// </param>
    public sealed record CachedAnswer(string Answer, string CachedQuestion, double Similarity);

    /// <summary>
    /// Số liệu của tầng cache ngữ nghĩa.
    /// </summary>
    /// <param name="Errors">
    /// Số lần thao tác cache thất bại và bị nuốt. BẮT BUỘC phải có: fail-open làm lỗi vô hình
    /// theo thiết kế, nên không đếm thì một Redis chết cả tuần trông y hệt một cache còn nguội —
    /// vẫn trả trọn tiền LLM mãi mãi trong khi số liệu báo một tỉ lệ 0% trông rất hợp lý.
    /// </param>
    public sealed record SemanticAnswerCacheStats(long Hits, long Misses, long Writes, long Errors)
    {
        public double HitRate => Hits + Misses == 0 ? 0d : (double)Hits / (Hits + Misses);
    }

    /// <summary>
    /// Cache câu trả lời theo NGỮ NGHĨA: khóa là vector câu hỏi, khớp bằng KNN chứ không bằng so
    /// chuỗi. Trúng thì cắt bỏ CẢ truy hồi Qdrant LẪN lượt gọi LLM.
    /// <para>
    /// BẤT BIẾN: không method nào ở đây được phép ném. Redis hỏng thì đường trả lời bình thường
    /// phải chạy tiếp như chưa có gì xảy ra (fail-open), nên cài đặt tự nuốt lỗi và ghi cảnh báo.
    /// Ngoại lệ duy nhất là <see cref="OperationCanceledException"/> của chính token caller:
    /// người chơi đã ngắt kết nối thì không có lý do gì để giữ request sống.
    /// </para>
    /// <para>
    /// Cố ý BẤT ĐỒNG BỘ, khác hẳn <see cref="INormalizationCache"/> và các interface anh em. Ba
    /// cái đó tra <c>MemoryCache</c> ngay trong tiến trình: dưới một micro-giây, không I/O, nên
    /// bọc <c>Task</c> vào chỉ là cấp phát vô ích (và <c>out</c> vốn không dùng được với
    /// <c>async</c>). Cái này đi qua MẠNG rồi duyệt vector. Làm nó đồng bộ nghĩa là chặn một
    /// thread của thread pool cho mỗi request, và khi Redis chậm thì thread pool cạn — hậu quả là
    /// chính các lượt gọi LLM (đều async) bắt đầu timeout, tức là cache đứng ra phá đúng con
    /// đường mà nó sinh ra để bảo vệ. Bất đồng bộ còn là cách duy nhất truyền được
    /// <see cref="CancellationToken"/> xuống dưới.
    /// </para>
    /// </summary>
    public interface ISemanticAnswerCache
    {
        /// <summary>
        /// Tìm câu trả lời đã lưu cho một câu hỏi ĐỦ GẦN về ngữ nghĩa.
        /// Trả <c>null</c> khi trượt, khi cache tắt, hoặc khi Redis không dùng được — ba trường
        /// hợp này cố ý không phân biệt được với nhau, vì caller phản ứng giống hệt nhau cả ba.
        /// </summary>
        Task<CachedAnswer?> TryGetAsync(SemanticAnswerQuery query, CancellationToken cancellationToken = default);

        /// <param name="hasContext">
        /// Lượt trả lời này có ngữ cảnh truy hồi hay không. Caller biết, cache thì không — cùng
        /// kiểu chia việc với tham số <c>unchanged</c> của <see cref="INormalizationCache"/>.
        /// Câu trả lời sinh ra từ ngữ cảnh RỖNG gần như luôn là "tôi không biết"; ghi nó lại là
        /// đóng băng một lần truy hồi hụt thành câu trả lời chính thức cho cả một chùm câu hỏi.
        /// </param>
        Task SetAsync(SemanticAnswerQuery query, string answer, bool hasContext, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Số liệu của tầng cache ngữ nghĩa. Tách khỏi <see cref="IQueryCacheStatistics"/> chứ không
    /// nhồi thêm trường vào <see cref="QueryCacheStats"/>: làm thế thì <c>NullQueryCache</c> và
    /// <c>MemoryQueryCache</c> — hai lớp không biết gì về Redis — vẫn buộc phải khai báo mấy con
    /// số của Redis và trả về 0.
    /// </summary>
    public interface ISemanticAnswerCacheStatistics
    {
        SemanticAnswerCacheStats GetStats();
    }

    /// <summary>
    /// Đường vận hành của cache ngữ nghĩa. Tách riêng vì xoá cache là mối quan tâm của người vận
    /// hành chứ không phải của đường trả lời — cùng cách chia <see cref="IRouteAdmin"/> với
    /// <see cref="IAskService"/>.
    /// <para>
    /// Tồn tại ngay từ đầu chứ không để sau: sẽ có lúc một câu NPC bị nhớ sai, và không có cái này
    /// thì cách chữa duy nhất là đợi hết hạn hoặc FLUSHALL cả Redis.
    /// </para>
    /// </summary>
    public interface ISemanticAnswerCacheAdmin
    {
        /// <summary>Xoá mọi entry của một phân vùng. Trả về số entry đã xoá.</summary>
        Task<long> PurgeAsync(string npcName, string npcPersona, CancellationToken cancellationToken = default);
    }
}
