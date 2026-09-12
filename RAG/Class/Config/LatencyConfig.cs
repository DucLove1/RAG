using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình đo độ trễ. Mọi ngưỡng và mọi khuôn dạng đều ở đây để chỉnh mà không phải build lại —
    /// cùng nguyên tắc với <see cref="QueryCacheConfig"/>.
    /// </summary>
    public class LatencyConfig
    {
        public const string SectionName = "Latency";

        /// <summary>
        /// Tắt thì DI dùng Null Object và KHÔNG bọc decorator nào cả, nên chi phí đúng bằng không —
        /// không phải một nhánh <c>if</c> nằm trong từng lời gọi. Cùng cách làm với
        /// <c>QueryCache:Enabled</c>.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Tổng thời gian vượt ngưỡng này thì dòng log lên mức Warning. Mặc định 5 giây: một request
        /// đi qua ba lượt LLM cộng một lượt nhúng thì 2-3 giây là bình thường, quá 5 giây là có một
        /// chặng đang hỏng chứ không phải đang chậm.
        /// </summary>
        [Range(0, int.MaxValue)]
        public int SlowThresholdMs { get; set; } = 5000;

        /// <summary>Format số mili-giây. "F1" là một chữ số thập phân — đủ để phân biệt 0,4ms với 0ms.</summary>
        [Required]
        public string MillisecondFormat { get; set; } = "F1";

        /// <summary>Khuôn một stage: {0} = tên, {1} = số ms.</summary>
        [Required]
        public string StageFormat { get; set; } = "{0}={1}ms";

        /// <summary>Khuôn một stage bị gọi nhiều lần: {0} = tên, {1} = tổng ms, {2} = số lần gọi.</summary>
        [Required]
        public string StageWithCallsFormat { get; set; } = "{0}={1}ms x{2}";

        public string StageSeparator { get; set; } = " | ";

        /// <summary>Khuôn một nhãn: {0} = tên, {1} = giá trị.</summary>
        [Required]
        public string TagFormat { get; set; } = "{0}={1}";

        public string TagSeparator { get; set; } = " ";

        /// <summary>
        /// Khuôn cả dòng log. Là message template của <c>ILogger</c> chứ không phải chuỗi
        /// <c>string.Format</c>, nên các placeholder có TÊN và bốn giá trị vẫn đi vào log structured
        /// dưới đúng tên đó thay vì bị nung thành một chuỗi phẳng.
        /// <para>
        /// Đổi khuôn này thì phải giữ nguyên đủ bốn placeholder và ĐÚNG THỨ TỰ — <c>ILogger</c> khớp
        /// tham số theo vị trí, không theo tên.
        /// </para>
        /// </summary>
        [Required]
        public string MessageTemplate { get; set; } = "Độ trễ [{Operation}] {TotalMs}ms | {Tags} | {Stages}";

        /// <summary>
        /// Tên các stage bỏ qua, lấy từ <c>LatencyStages</c>. Có mặt để tắt bớt phần ồn khi đang soi
        /// một chặng cụ thể, không phải build lại. Tên không khớp stage nào thì không có tác dụng gì
        /// và cũng không phải lỗi — danh sách stage thay đổi theo phiên bản.
        /// </summary>
        public string[] DisabledStages { get; set; } = Array.Empty<string>();
    }
}
