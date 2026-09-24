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

        /// <summary>
        /// Mã chunk <c>&lt;doc_id&gt;#L&lt;n&gt;</c> — KHÓA GHÉP với đồ thị tri thức. Xem
        /// <see cref="ChunkCodes"/>.
        /// <para>
        /// Chỉ có mặt ở điểm do chiến lược cắt theo dòng sinh ra, nên MỌI chỗ đọc trường này phải
        /// dùng <c>TryGetValue</c> chứ không dùng indexer: điểm nạp bằng chiến lược cắt theo câu
        /// không có nó, và những điểm đó vẫn hợp lệ.
        /// </para>
        /// </summary>
        public const string ChunkCode = "chunkCode";

        /// <summary>Mã tài liệu. Thứ duy nhất đối chiếu được với <c>duoc_biet</c> của NPC trong đồ thị.</summary>
        public const string DocId = "docId";

        /// <summary>Loại tài liệu, lấy nguyên từ front matter. Chỉ để truy vết và lọc về sau.</summary>
        public const string Kind = "loai";

        /// <summary>
        /// Các màn chơi mà tài liệu thuộc về, ghép thành chuỗi.
        /// <para>
        /// Ghi vào payload nhưng CHƯA được dùng để lọc. Nó ở đây để dành: game có nhiều màn, và một
        /// NPC ở màn 2 không nên trả lời bằng tri thức chỉ xuất hiện ở màn 5. Thêm bộ lọc đó sau này
        /// sẽ không phải nạp lại toàn bộ corpus.
        /// </para>
        /// </summary>
        public const string Scene = "scene";

        public const string Title = "tieu_de";
    }
}
