namespace RAG.Interface
{
    /// <summary>
    /// Render các mối liên hệ của đồ thị thành một khối văn bản cho prompt.
    /// <para>
    /// Tách khỏi <see cref="IAskContextBuilder"/> vì render cần <see cref="IOntology"/> để lấy nhãn
    /// tiếng Việt, mà ontology chỉ tồn tại khi đồ thị bật. Khi tắt, composition root đăng ký bản
    /// Null trả chuỗi rỗng — tầng dựng ngữ cảnh không cần biết đồ thị có hay không.
    /// </para>
    /// </summary>
    public interface IGraphContextRenderer
    {
        /// <summary>
        /// Render theo đúng thứ tự nhận vào, dừng ở cạnh đầu tiên làm vượt <paramref name="budgetChars"/>.
        /// Không cạnh nào vừa thì trả chuỗi rỗng — không bao giờ trả một tiêu đề trơ trọi.
        /// </summary>
        /// <returns>Khối đã render và số cạnh thực sự nằm trong đó.</returns>
        (string Block, int RelationCount) Render(IReadOnlyList<GraphRelation> relations, int budgetChars);
    }
}
