namespace RAG.Class.Constants
{
    /// <summary>
    /// Các hằng số thuộc về giao thức của Google Generative Language API.
    /// Đây là ràng buộc của nhà cung cấp (không phải cấu hình người dùng) nên khai báo dưới dạng hằng.
    /// </summary>
    public static class GeminiApiDefaults
    {
        public const string ApiKeyHeader = "x-goog-api-key";
        public const string UserRole = "user";
    }
}
