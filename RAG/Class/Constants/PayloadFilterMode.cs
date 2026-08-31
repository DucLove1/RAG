namespace RAG.Class.Constants
{
    /// <summary>
    /// Cách khớp giá trị payload khi lọc kết quả truy hồi.
    /// <para>
    /// Trước đây <c>MatchPhrase</c> nằm cứng trong pipeline. Ba kiểu này cho ra kết quả khác nhau
    /// rõ rệt với tên NPC nhiều từ, nên đây là thứ cần thử được mà không phải build lại.
    /// </para>
    /// </summary>
    public enum PayloadFilterMode
    {
        /// <summary>Khớp đúng cụm từ, theo thứ tự. Phù hợp với tên NPC nhiều từ.</summary>
        Phrase = 0,

        /// <summary>Khớp nguyên vẹn cả giá trị, không tách từ.</summary>
        Keyword = 1,

        /// <summary>Khớp toàn văn: đủ các từ là được, không cần đúng thứ tự.</summary>
        Text = 2
    }
}
