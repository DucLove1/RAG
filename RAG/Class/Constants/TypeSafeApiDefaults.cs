namespace RAG.Class.Constants
{
    /// <summary>
    /// Literal thuộc giao thức của TypeSafe System One API — cố định theo nhà cung cấp, không phải
    /// núm chỉnh vận hành, nên nằm ở đây thay vì configuration (giống <see cref="GeminiApiDefaults"/>).
    /// </summary>
    public static class TypeSafeApiDefaults
    {
        /// <summary>Scheme của header <c>Authorization</c>.</summary>
        public const string AuthScheme = "Bearer";

        /// <summary>Giá trị <c>type</c> của câu hỏi chọn một option trong tập cho trước.</summary>
        public const string ChoiceQuestionType = "choice";

        /// <summary>Số option tối đa một câu hỏi Choice chấp nhận (giới hạn của API).</summary>
        public const int MaxChoiceOptions = 255;
    }
}
