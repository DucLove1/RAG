namespace RAG.Class.Constants
{
    /// <summary>
    /// Tên các trường payload lưu trong Qdrant. Tập trung một chỗ để tránh lặp chuỗi rời rạc.
    /// </summary>
    public static class PayloadFields
    {
        public const string NpcNames = "npcNames";
        public const string Text = "text";
        public const string Source = "source";
    }
}
