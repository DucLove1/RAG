using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình đường nạp tri thức.
    /// </summary>
    public class IngestionConfig
    {
        public const string SectionName = "Ingestion";

        /// <summary>
        /// Các phần mở rộng được coi là văn bản thuần. Viết chữ thường, có dấu chấm đầu.
        /// </summary>
        [MinLength(1)]
        public List<string> PlainTextExtensions { get; set; } = new() { ".txt", ".json" };

        /// <summary>
        /// Các phần mở rộng có khối YAML front matter cần bóc trước khi cắt đoạn.
        /// <para>
        /// PHẢI rời nhau với <see cref="PlainTextExtensions"/>. Bộ nạp chọn bộ đọc ĐẦU TIÊN nhận
        /// phần mở rộng, nên để <c>.md</c> ở cả hai danh sách sẽ làm kết quả phụ thuộc vào thứ tự
        /// đăng ký trong composition root — và khi bộ đọc văn bản thuần thắng thì bảy dòng front
        /// matter lặng lẽ biến thành bảy đoạn tri thức giả, đẩy lệch toàn bộ mã chunk phía sau.
        /// </para>
        /// </summary>
        public List<string> FrontMatterExtensions { get; set; } = new() { ".md" };

        /// <summary>
        /// Thư mục corpus dùng cho endpoint nạp cả thư mục, tương đối so với thư mục chạy.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string CorpusPath { get; set; } = "corpus";

        /// <summary>
        /// Sinh id điểm từ mã chunk thay vì ngẫu nhiên, nhờ đó nạp lại cùng một corpus là GHI ĐÈ
        /// chứ không phải nhân đôi. Chỉ có tác dụng với đoạn CÓ mã chunk.
        /// </summary>
        public bool DeterministicChunkIds { get; set; } = true;
    }
}
