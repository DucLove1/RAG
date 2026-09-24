namespace RAG.Class.Constants
{
    /// <summary>
    /// Chiến lược cắt đoạn đang dùng. Dùng enum thay cho magic string để bind thẳng từ configuration
    /// và không sai chính tả được, cùng mẫu với <see cref="SemanticRouterStrategy"/>.
    /// <para>
    /// Chọn bằng một <c>switch</c> trong composition root chứ không phải Keyed Services: chỉ đúng
    /// MỘT chiến lược tồn tại tại một thời điểm, giống <see cref="AnswerCacheProvider"/> và khác
    /// <see cref="LlmProviderKey"/> (nơi hai provider sống cùng lúc).
    /// </para>
    /// </summary>
    public enum ChunkingStrategyKind
    {
        /// <summary>
        /// Cắt theo kích thước cố định rồi co lại tới dấu kết câu gần nhất. Dành cho văn xuôi dài
        /// chưa được chuẩn bị trước. Đoạn cắt ra KHÔNG ánh xạ được về một dòng nào, nên nó không có
        /// mã chunk và đồ thị không bao giờ nối tới được.
        /// </summary>
        Sentence = 0,

        /// <summary>
        /// Mỗi dòng không trắng là một đoạn. Dành cho corpus đã được viết sao cho một dòng là một
        /// đơn vị ngữ nghĩa trọn vẹn — và là chiến lược DUY NHẤT sinh ra mã chunk, tức là điều kiện
        /// bắt buộc để bật đồ thị tri thức.
        /// </summary>
        Line = 1
    }
}
