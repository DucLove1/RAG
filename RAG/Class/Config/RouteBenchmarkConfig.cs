using RAG.Class.Constants;
using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình benchmark so sánh các chiến lược định tuyến trên một bộ câu hỏi cố định (file CSV).
    /// <para>
    /// Bật benchmark nghĩa là đăng ký THÊM phần phụ thuộc của mọi chiến lược trong
    /// <see cref="Strategies"/> — kể cả key TypeSafe khi có Jev — nên chỉ mở ở Development.
    /// </para>
    /// </summary>
    public class RouteBenchmarkConfig
    {
        public const string SectionName = "RouteBenchmark";

        /// <summary>Có mở endpoint benchmark hay không (còn phải chạy ở Development).</summary>
        public bool Enabled { get; set; }

        /// <summary>Các chiến lược được chạy, theo đúng thứ tự này.</summary>
        [MinLength(1)]
        public List<SemanticRouterStrategy> Strategies { get; set; } = new();

        /// <summary>File CSV chứa bộ câu hỏi, giải theo thư mục output của app.</summary>
        [Required]
        public string CasesPath { get; set; } = string.Empty;

        /// <summary>Nhãn đại diện cho "không route nào khớp" (đi đường RAG), dùng cả trong CSV lẫn kết quả.</summary>
        [Required]
        public string NoRouteLabel { get; set; } = string.Empty;

        /// <summary>Chuẩn hóa câu hỏi MỘT lần trước khi đưa cho mọi chiến lược, giống đường trả lời thật.</summary>
        public bool NormalizeQuestions { get; set; } = true;

        /// <summary>Nghỉ giữa hai lượt gọi router, để không đụng giới hạn tần suất của nhà cung cấp.</summary>
        [Range(0, int.MaxValue)]
        public int DelayBetweenCallsMilliseconds { get; set; }

        /// <summary>
        /// Câu gọi thử MỘT lần cho mỗi chiến lược trước khi bắt đầu đo, kết quả bỏ đi. Lượt gọi đầu
        /// tiên gánh DNS/TLS handshake (và với LLM là dựng client SDK) — không làm nóng thì câu đầu
        /// bộ benchmark bị phạt oan, và phạt mỗi router một mức khác nhau. Để trống thì bỏ qua.
        /// </summary>
        public string WarmupQuestion { get; set; } = string.Empty;

        /// <summary>
        /// Xoay vòng thứ tự chiến lược theo từng câu (câu 0: A→B, câu 1: B→A, ...). Chạy cố định một
        /// thứ tự thì một router luôn đi trước — luôn gánh trước mọi dao động mạng và hạn mức —
        /// nên số đo bị thiên lệch có hệ thống.
        /// </summary>
        public bool AlternateOrder { get; set; } = true;

        /// <summary>Thông điệp khi gọi benchmark trong lúc nó đang tắt.</summary>
        public string DisabledMessage { get; set; } = string.Empty;
    }
}
