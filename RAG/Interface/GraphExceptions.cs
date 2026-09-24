namespace RAG.Interface
{
    /// <summary>
    /// Dữ liệu đồ thị viết tay không hợp lệ so với ontology hoặc so với corpus.
    /// <para>
    /// Mang DANH SÁCH vi phạm chứ không phải vi phạm đầu tiên: sửa dữ liệu là việc lặp, và bắt
    /// người soạn chạy lại bộ nạp một lần cho mỗi lỗi chính tả là cách chắc chắn nhất để họ bỏ
    /// cuộc giữa chừng.
    /// </para>
    /// <para>
    /// Ném TRƯỚC khi ghi một dòng nào xuống Neo4j. Ghi một nửa rồi mới phát hiện sai nghĩa là để
    /// lại một đồ thị không ai biết đang ở trạng thái nào — mà bộ nạp thì không có DETACH DELETE
    /// để dọn.
    /// </para>
    /// </summary>
    public sealed class GraphDataInvalidException : Exception
    {
        public GraphDataInvalidException(string message, IReadOnlyList<string> violations) : base(message) =>
            Violations = violations;

        public IReadOnlyList<string> Violations { get; }
    }

    /// <summary>
    /// Gọi một thao tác quản trị đồ thị trong khi nó đang tắt hoặc đang chạy ngoài Development.
    /// <para>
    /// Ánh xạ thành 404 chứ không phải 403: endpoint quản trị mà trả 403 là đã xác nhận nó tồn tại.
    /// </para>
    /// </summary>
    public sealed class GraphAdminDisabledException : Exception
    {
        public GraphAdminDisabledException(string message) : base(message) { }
    }

    /// <summary>
    /// Neo4j nhận được câu lệnh nhưng TỪ CHỐI nó: sai cú pháp, xung đột schema, vi phạm ràng buộc.
    /// <para>
    /// Tách hẳn khỏi <see cref="GraphUnavailableException"/> vì gộp chung là đổ oan. Lần đầu bộ nạp
    /// chạy trên một database đã có sẵn index cũ, Neo4j trả về "There already exists an index
    /// (:ThucThe {{ten}})" — một câu nói rõ phải làm gì — nhưng nó bị gói vào thông báo "kiểm lại
    /// NEO4J__PASSWORD" và người vận hành đi tìm lỗi ở đúng chỗ không có lỗi.
    /// </para>
    /// <para>
    /// Vì vậy <see cref="Exception.Message"/> ở đây mang NGUYÊN VĂN câu Neo4j trả về. Server đã nói
    /// đúng thứ cần nghe rồi; việc của lớp này là đừng che nó đi.
    /// </para>
    /// </summary>
    public sealed class GraphStatementRejectedException : Exception
    {
        public GraphStatementRejectedException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Không nói chuyện được với Neo4j trên ĐƯỜNG QUẢN TRỊ.
    /// <para>
    /// Chỉ đường quản trị mới ném cái này. Đường trả lời KHÔNG BAO GIỜ để nó thoát ra: ở đó đồ thị
    /// chết phải suy biến lặng lẽ về RAG thuần, vì người chơi không có gì để làm với một mã 503.
    /// </para>
    /// </summary>
    public sealed class GraphUnavailableException : Exception
    {
        public GraphUnavailableException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
