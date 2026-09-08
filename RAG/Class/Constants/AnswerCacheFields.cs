namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên field và các hằng số giao thức của index cache ngữ nghĩa trên RediSearch.
    /// <para>
    /// Đây là hằng số GIAO THỨC chứ không phải cấu hình người dùng: đổi một giá trị ở đây là đổi
    /// lược đồ index, nên phải kèm theo việc tạo lại index chứ không thể chỉnh lúc chạy. Gom vào
    /// một chỗ vì mỗi tên field xuất hiện ở ít nhất ba nơi — lược đồ FT.CREATE, chuỗi truy vấn
    /// FT.SEARCH, và lệnh HSET — mà gõ lệch một chỗ thì không có lỗi biên dịch nào bắt được.
    /// </para>
    /// </summary>
    public static class AnswerCacheFields
    {
        /// <summary>Tag phân vùng: băm của (tên NPC + mô tả tính cách). Xem RedisSemanticAnswerCache.Tag.</summary>
        public const string Npc = "npc";

        /// <summary>Vector câu hỏi, FLOAT32 little-endian. Đây là field được đánh chỉ mục KNN.</summary>
        public const string Vector = "vector";

        /// <summary>Câu hỏi nguyên văn. Chỉ để đọc bằng mắt và ghi log; không được index, không được truy vấn.</summary>
        public const string Question = "question";

        /// <summary>Câu trả lời do LLM sinh ra — thứ mà cả tầng cache này tồn tại để trả về.</summary>
        public const string Answer = "answer";

        /// <summary>
        /// Bí danh của điểm KNN trong truy vấn. Hai gạch dưới ở đầu để không thể trùng với một
        /// field thật nếu lược đồ được mở rộng sau này.
        /// </summary>
        public const string Score = "__score";

        /// <summary>Tên tham số mang vector truy vấn trong mệnh đề PARAMS.</summary>
        public const string BlobParam = "BLOB";

        /// <summary>
        /// Số ký tự hex giữ lại của một tag đã băm. 32 ký tự = 128 bit.
        /// Đừng rút ngắn để tiết kiệm byte: hậu quả của va chạm là một NPC trả lời bằng câu của
        /// NPC khác, tức là rò dữ liệu chứ không phải chậm đi.
        /// </summary>
        public const int TagLength = 32;

        /// <summary>
        /// DIALECT của truy vấn. Cú pháp KNN kèm bộ lọc trước ("(filter)=>[KNN ...]") CHỈ tồn tại
        /// từ dialect 2; chạy ở dialect 1 thì RediSearch coi cả chuỗi là văn bản cần tìm và trả về
        /// rỗng, không báo lỗi.
        /// </summary>
        public const int Dialect = 2;


        /// <summary>
        /// Tên client báo cho Redis biết ai đang nối. Hiện ra trong CLIENT LIST, nên khi một
        /// instance Redis dùng chung cho nhiều thứ thì vẫn nhìn ra kết nối nào là của cache này.
        /// </summary>
        public const string ClientName = "rag-answer-cache";

        /// <summary>Số kết quả lấy về. Chỉ cần láng giềng gần nhất — lấy nhiều hơn là tải về những câu trả lời chắc chắn bị bỏ.</summary>
        public const int NeighborCount = 1;
    }
}
