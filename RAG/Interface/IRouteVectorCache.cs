namespace RAG.Interface
{
    /// <summary>
    /// Lưu lại vector của các câu mẫu để lần khởi động sau không phải gọi lại API embedding.
    /// Cùng một câu luôn cho ra cùng một vector, nên tính lại mỗi lần khởi động là việc thừa
    /// và tốn quota của nhà cung cấp.
    /// </summary>
    public interface IRouteVectorCache
    {
        /// <summary>
        /// Nạp vector đã lưu nếu vân tay khớp; trả <c>null</c> khi chưa có cache hoặc cache đã cũ.
        /// Khóa của dictionary là chính câu mẫu, nhờ đó đổi tên route hay chuyển một câu mẫu
        /// sang route khác không làm mất cache.
        /// Mọi lỗi đọc (thiếu quyền, JSON hỏng) đều được nuốt và coi như cache miss.
        /// </summary>
        Task<IReadOnlyDictionary<string, float[]>?> TryLoadAsync(string fingerprint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ghi lại toàn bộ vector kèm vân tay. Mọi lỗi ghi đều được nuốt: cache là tối ưu hóa,
        /// không được phép làm hỏng quá trình khởi động.
        /// </summary>
        Task SaveAsync(string fingerprint, IReadOnlyDictionary<string, float[]> vectors, CancellationToken cancellationToken = default);
    }
}
