namespace RAG.Interface
{
    public interface IEmbeddingProvider
    {
        /// <summary>
        /// Định danh model đang dùng. Cần cho việc tính vân tay cache: hai model khác nhau có thể
        /// cùng số chiều nhưng cho ra vector hoàn toàn khác, nên nếu chỉ băm số chiều thì đổi model
        /// sẽ âm thầm dùng lại vector cũ.
        /// </summary>
        string ModelId { get; }

        Task<float[]> GetEmbeddingsAsync(string input, CancellationToken cancellationToken = default);

        /// <summary>
        /// Nhúng nhiều câu trong ít lần gọi mạng nhất có thể.
        /// <para>
        /// BẤT BIẾN: kết quả trả về ĐÚNG SỐ LƯỢNG và ĐÚNG THỨ TỰ như đầu vào, vì caller ghép
        /// kết quả với câu gốc theo chỉ số. Câu nào không nhúng được thì phần tử tương ứng là
        /// mảng rỗng, để caller tự quyết định bỏ hay giữ.
        /// </para>
        /// Việc chia lô là chi tiết cài đặt của từng provider; caller không cần biết giới hạn của API.
        /// </summary>
        Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);

        Task<int> GetDimsAsync();
    }
}
