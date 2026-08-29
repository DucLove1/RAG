namespace RAG.Interface
{
    /// <summary>
    /// Kết quả của một lần thêm câu mẫu lúc chạy.
    /// </summary>
    /// <param name="Success">Có thêm được câu nào không.</param>
    /// <param name="Message">Mô tả cho người gọi, kể cả khi thất bại.</param>
    /// <param name="Added">Số câu mẫu thực sự được thêm.</param>
    /// <param name="Skipped">Số câu bị bỏ vì trùng lặp hoặc vector không hợp lệ.</param>
    /// <param name="TotalInRoute">Tổng số câu mẫu của route sau khi thêm.</param>
    /// <param name="Persisted">
    /// Thay đổi đã được ghi xuống đĩa hay chưa. Nếu <c>false</c>, route vẫn hoạt động ngay
    /// nhưng sẽ mất khi khởi động lại.
    /// </param>
    public sealed record RouteUpdateResult(
        bool Success,
        string Message,
        int Added,
        int Skipped,
        int TotalInRoute,
        bool Persisted);
}
