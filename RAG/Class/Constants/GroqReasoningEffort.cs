namespace RAG.Class.Constants
{
    /// <summary>
    /// Mức suy nghĩ (reasoning_effort) của Groq. Tập giá trị hợp lệ phụ thuộc model:
    /// dòng gpt-oss chỉ nhận <see cref="Low"/> / <see cref="Medium"/> / <see cref="High"/> (không tắt hẳn được),
    /// dòng Qwen3 nhận <see cref="None"/> (tắt) / <see cref="Default"/> (bật).
    /// </summary>
    public enum GroqReasoningEffort
    {
        None = 0,
        Default = 1,
        Low = 2,
        Medium = 3,
        High = 4
    }
}
