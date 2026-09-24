namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên các thuộc tính hệ thống trên node và cạnh của đồ thị.
    /// <para>
    /// Đây là NGOẠI LỆ DUY NHẤT của luật "C# không được biết ontology có gì", và ngoại lệ có lý do:
    /// Cypher không nhận tên thuộc tính làm tham số một cách sạch sẽ (<c>n[$prop]</c> dùng được
    /// nhưng không index được và không đọc nổi), còn mấy tên này là HỢP ĐỒNG giữa app và bộ nạp
    /// chứ không phải từ vựng của vụ án. Đổi <c>ten</c> thành <c>name</c> là một cuộc di trú schema,
    /// không phải một lần soạn lại tri thức — khác hẳn việc thêm một loại quan hệ.
    /// </para>
    /// <para>
    /// Cùng vai trò với <see cref="PayloadFields"/> ở phía kho vector.
    /// </para>
    /// </summary>
    public static class GraphProperties
    {
        /// <summary>Tên thực thể. Là khóa định danh, có constraint UNIQUE.</summary>
        public const string Name = "ten";

        /// <summary>Danh sách mã chunk mà node hoặc cạnh này tựa vào. Khóa ghép với kho vector.</summary>
        public const string SourceChunks = "nguon_chunk";

        /// <summary>Độ tin cậy của một cạnh.</summary>
        public const string Status = "trang_thai";

        /// <summary>Danh sách MÃ TÀI LIỆU mà một NPC được đọc. Chỉ có trên node mang nhãn NPC.</summary>
        public const string KnownDocs = "duoc_biet";

        /// <summary>Tên gọi khác, phục vụ tra cứu toàn văn khi người chơi gõ một cách gọi khác.</summary>
        public const string Aliases = "bi_danh";

        /// <summary>
        /// Cạnh này PHỦ ĐỊNH quan hệ của nó: <c>Danie -CO_DAU_VET_CAM(phu_dinh)-> Khẩu súng</c>
        /// nghĩa là Danie KHÔNG có dấu vết cầm súng.
        /// <para>
        /// Là thuộc tính hệ thống chứ không phải một loại quan hệ riêng, vì phủ định cắt ngang mọi
        /// loại: corpus có 7 chỗ phủ định trải khắp lời khai, khám nghiệm và ngoại phạm. Đặt tên
        /// <c>KHONG_CO_DAU_VET_CAM</c> cho từng cái sẽ nhân đôi bảng từ vựng và làm mọi truy vấn
        /// "ai có dấu vết cầm súng" phải nhớ liệt kê cả biến thể phủ định.
        /// </para>
        /// <para>
        /// Thiếu nó thì <c>25_phap-y#L3</c> — <i>"trên tay Danie KHÔNG có dấu hiệu cầm súng"</i> —
        /// vào đồ thị thành lời khẳng định ngược hẳn, mang <c>trang_thai: xac_nhan</c>, và NPC pháp y
        /// sẽ nói với người chơi đúng điều trái với kết quả khám nghiệm của chính mình.
        /// </para>
        /// </summary>
        public const string Negated = "phu_dinh";
    }
}
