namespace RAG.Class.Constants
{
    /// <summary>
    /// Kiểu truy hồi trên đồ thị tri thức. Dùng enum thay cho magic string để bind thẳng từ
    /// configuration và không sai chính tả được, cùng mẫu với <see cref="SemanticRouterStrategy"/>.
    /// <para>
    /// Hiện chỉ có một giá trị, và trường <c>Graph:Mode</c> vẫn tồn tại là CÓ CHỦ ĐÍCH: nó giữ sẵn
    /// chỗ cho global search. Khi giá trị thứ hai xuất hiện, hai cài đặt của <c>IGraphSearch</c> sẽ
    /// SỐNG CÙNG LÚC (chế độ tự động chạy local rồi lùi về global khi rỗng), nên lúc đó đây trở
    /// thành khóa của Keyed Services — khác hẳn <see cref="AnswerCacheProvider"/>, nơi chỉ đúng một
    /// provider tồn tại nên việc chọn nằm ở một <c>switch</c>.
    /// </para>
    /// </summary>
    public enum GraphSearchMode
    {
        /// <summary>
        /// Local search: LLM chọn trong danh mục thực thể NPC được biết những thực thể mà câu hỏi
        /// nhắc tới, rồi mở rộng một bậc quanh chúng trong khuôn khổ tri thức của NPC đó — chạy song
        /// song với truy hồi vector, không lấy hạt giống từ nó. Đây là bản tương đương local search
        /// của Microsoft GraphRAG.
        /// </summary>
        Local = 0
    }
}
