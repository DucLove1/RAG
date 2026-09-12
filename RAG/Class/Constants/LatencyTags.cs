namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên các nhãn gắn kèm một phiên đo. Nhãn trả lời câu "vì sao phiên này lại có đúng những stage
    /// đó" — thiếu chúng thì một phiên chỉ có ba stage trông giống hệt một phiên bị lỗi giữa chừng.
    /// </summary>
    public static class LatencyTags
    {
        /// <summary>Nhánh nào của AskPipeline đã chạy. Suy ra từ các nhãn dưới đây.</summary>
        public const string Branch = "branch";

        public const string Npc = "npc";

        /// <summary>Tên route đã khớp, hoặc <see cref="None"/>.</summary>
        public const string Route = "route";

        /// <summary>Kết quả tra cache ngữ nghĩa: <see cref="Hit"/> / <see cref="Miss"/>.</summary>
        public const string AnswerCache = "answerCache";

        public const string WeakPointHit = "weakPointHit";

        /// <summary>Số kết quả Qdrant trả về. Truy hồi 0 kết quả là nguyên nhân số một của câu trả lời "tôi không biết".</summary>
        public const string Hits = "hits";
    }

    /// <summary>
    /// Giá trị của nhãn. Là hằng chứ không phải chuỗi trần vì chúng vừa được GHI ở decorator này vừa
    /// được SO SÁNH ở decorator khác (<c>TimedAskService</c> đọc nhãn route và weak-point để suy ra
    /// nhánh) — hai chỗ lệch nhau một chữ thì nhánh luôn bị đoán sai mà không có gì báo.
    /// </summary>
    public static class LatencyTagValues
    {
        public const string None = "none";
        public const string Hit = "hit";
        public const string Miss = "miss";
        public const string True = "true";
        public const string False = "false";

        // Ba nhánh của AskPipeline.
        public const string BranchRouted = "routed";
        public const string BranchWeakPoint = "weakpoint";
        public const string BranchRetrieval = "retrieval";
    }
}
