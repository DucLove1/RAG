using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Tham số của bộ cắt đoạn. Bản trước để toàn bộ mấy con số này nằm cứng trong thân hàm
    /// (khoảng nhìn lại 50 ký tự, chia 5, chia 2, tập ký tự kết câu), nên tinh chỉnh cách cắt đoạn
    /// là phải sửa code.
    /// </summary>
    public class ChunkingConfig
    {
        public const string SectionName = "Chunking";

        /// <summary>Các ký tự được coi là kết thúc câu, dùng để cắt cho gọn ý.</summary>
        [Required]
        [MinLength(1)]
        public string SentenceEndings { get; set; } = ".!?\n";

        /// <summary>
        /// Số ký tự tối thiểu được quét ngược để tìm dấu kết câu, kể cả khi đoạn rất ngắn.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int MinLookback { get; set; } = 50;

        /// <summary>
        /// Khoảng nhìn lại tính theo tỉ lệ độ dài đoạn; giá trị thực là giá trị lớn hơn giữa nó
        /// và <see cref="MinLookback"/>.
        /// </summary>
        [Range(0.0, 1.0)]
        public double LookbackRatio { get; set; } = 0.2;

        /// <summary>
        /// Đoạn sau khi co lại phải dài ít nhất bằng tỉ lệ này so với kích thước đoạn mong muốn.
        /// Không có ràng buộc này thì một dấu chấm nằm sớm sẽ tạo ra đoạn cụt ngủn.
        /// </summary>
        [Range(0.0, 1.0)]
        public double MinChunkRatio { get; set; } = 0.5;
    }
}
