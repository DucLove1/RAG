namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên các khóa trong khối YAML front matter của corpus, và các giá trị có ý nghĩa với ứng dụng.
    /// <para>
    /// Là hằng GIAO THỨC với file nguồn, cùng loại với <see cref="ChunkCodes"/>: đổi một khóa ở đây
    /// là đổi định dạng của corpus, không phải tinh chỉnh một tham số. Vì vậy chúng không nằm trong
    /// <c>appsettings.json</c>.
    /// </para>
    /// </summary>
    public static class FrontMatterFields
    {
        /// <summary>Mã tài liệu. Là NGUỒN SỰ THẬT của phần trước dấu <c>#</c> trong mã chunk — tên file chỉ là phương án dự phòng.</summary>
        public const string DocId = "doc_id";

        public const string Title = "tieu_de";

        /// <summary>
        /// Ai nói ra tri thức trong tài liệu này. Đây cũng chính là danh sách NPC được biết nó, tức
        /// chính là bảng "Nhân vật dùng file nào" trong HUONG_DAN.md.
        /// </summary>
        public const string Source = "nguon";

        /// <summary>Loại tài liệu. Xem <see cref="DocumentKinds"/>.</summary>
        public const string Kind = "loai";

        /// <summary>Các màn chơi mà tài liệu này thuộc về.</summary>
        public const string Scene = "scene";

        public const string Confidence = "do_tin_cay";

        public const string Summary = "tom_tat";

        /// <summary>Dấu ngăn khối front matter, theo đúng quy ước YAML.</summary>
        public const string Delimiter = "---";

        /// <summary>Dấu ngăn giữa các tên trong <see cref="Source"/> và khi ghép lại vào payload.</summary>
        public const string SourceSeparator = ", ";
    }

    /// <summary>
    /// Các giá trị của <see cref="FrontMatterFields.Kind"/> mà ứng dụng thật sự phân biệt.
    /// <para>
    /// CHỈ <see cref="Background"/> có ý nghĩa với code: nó là tài liệu mọi NPC đều biết, nên nó là
    /// một nhánh trong luật phân quyền. Các giá trị còn lại (<c>loi_khai_npc</c>,
    /// <c>ket_luan_chuyen_mon</c>, <c>tin_don</c>) chỉ đi vào payload để truy vết — chúng KHÔNG được
    /// ánh xạ máy móc sang <c>trang_thai</c> của cạnh trong đồ thị. Lý do nằm ở
    /// <c>10_chung_canh-sat</c>: nó mang <c>ket_luan_chuyen_mon</c> nhưng nội dung lại là kết luận
    /// SAI mà cả vụ án xoay quanh, nên ánh xạ máy móc sẽ đóng dấu "đã xác nhận" lên một đáp án giả.
    /// Trạng thái của từng cạnh do người soạn ghi tường minh trong dữ liệu đồ thị.
    /// </para>
    /// </summary>
    public static class DocumentKinds
    {
        public const string Background = "boi_canh";
    }
}
