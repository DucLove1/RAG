namespace RAG.Class.Constants
{
    /// <summary>
    /// Các hằng số thuộc về giao thức của Groq mà OpenAI SDK không có sẵn.
    /// Đây là ràng buộc của nhà cung cấp (không phải cấu hình người dùng) nên khai báo dưới dạng hằng.
    /// </summary>
    public static class GroqApiDefaults
    {
        public const string ReasoningEffortDefault = "default";
    }
}
