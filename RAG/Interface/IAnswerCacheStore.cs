namespace RAG.Interface
{
    /// <summary>
    /// Một entry cache câu trả lời ở dạng có thể ghi xuống đĩa.
    /// </summary>
    /// <param name="Tag">Giá trị phân vùng đã băm, xem <c>AnswerCachePartition.Tag</c>.</param>
    /// <param name="QuestionHash">
    /// Danh tính của câu hỏi trong phân vùng. Có mặt trong file chứ không tính lại lúc nạp: nó là
    /// khóa chống trùng khi nạp, và tính lại nghĩa là quy tắc băm phải khớp tuyệt đối giữa lần ghi
    /// và lần đọc — một ràng buộc không cần thiết khi chỉ cần chép 32 ký tự.
    /// </param>
    /// <param name="Vector">
    /// ĐÃ chuẩn hóa L2, xem <c>VectorMath.L2Normalize</c>. Lưu bản đã chuẩn hóa là an toàn vì
    /// cosine bất biến với phép nhân vô hướng, và nhờ vậy lúc nạp lại không phải chuẩn hóa lần
    /// hai — một bước ít đi là một bước ít có thể quên.
    /// </param>
    public sealed record StoredAnswer(string Tag,
                                      string QuestionHash,
                                      string Question,
                                      string Answer,
                                      float[] Vector,
                                      DateTime ExpiresAtUtc,
                                      DateTime LastAccessUtc);

    /// <param name="Dimensions">
    /// Số chiều chung của mọi vector trong ảnh chụp. Ghi một lần ở đầu khối thay vì lặp lại theo
    /// từng entry — cùng cách <c>QueryCacheSnapshot</c> được ghi.
    /// </param>
    public sealed record AnswerCacheSnapshot(IReadOnlyList<StoredAnswer> Entries, int Dimensions);

    /// <summary>
    /// Nơi lưu cache câu trả lời xuống đĩa.
    /// <para>
    /// BẤT BIẾN: mọi lỗi đọc/ghi đều bị NUỐT và chỉ ghi log. Cache là tối ưu hóa, không phải nguồn
    /// sự thật — cùng luật mà <see cref="IQueryCacheStore"/> áp dụng.
    /// </para>
    /// <para>
    /// Chỉ có nghĩa với provider chạy trong tiến trình. Provider Redis không dùng interface này:
    /// ở đó chính Redis là nơi lưu, và tính bền vững do RDB của nó lo.
    /// </para>
    /// </summary>
    public interface IAnswerCacheStore
    {
        /// <summary>
        /// Đọc ảnh chụp từ đĩa. Trả <c>null</c> khi chưa có file, file sai định dạng, file hỏng,
        /// hoặc vân tay đã cũ — bốn trường hợp này cố ý không phân biệt được, vì caller phản ứng
        /// giống hệt nhau cả bốn: bắt đầu với cache rỗng.
        /// </summary>
        Task<AnswerCacheSnapshot?> LoadAsync(string fingerprint, CancellationToken cancellationToken = default);

        /// <summary>Ghi ảnh chụp xuống đĩa. Trả <c>false</c> khi không ghi được.</summary>
        Task<bool> SaveAsync(string fingerprint, AnswerCacheSnapshot snapshot, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Mặt BỀN VỮNG của cache câu trả lời, tách khỏi mặt tra cứu.
    /// Cùng cách chia <see cref="IPersistableQueryCache"/> / <see cref="IQueryCacheStore"/>:
    /// đường trả lời chỉ thấy <see cref="ISemanticAnswerCache"/>, còn service ghi đĩa chỉ thấy
    /// cái này.
    /// </summary>
    public interface IPersistableAnswerCache
    {
        /// <summary>
        /// Vân tay của dữ liệu đang nằm trong cache (model nhúng + số chiều + phiên bản định dạng).
        /// Đây là thứ thay thế cần gạt <c>IndexName v1 -&gt; v2</c> của provider Redis: vân tay
        /// lệch thì file cũ bị bỏ qua TỰ ĐỘNG, thay vì phải nhớ bump một chuỗi bằng tay.
        /// </summary>
        string Fingerprint { get; }

        /// <summary>
        /// Đếm MỌI thay đổi, không chỉ lần ghi.
        /// <para>
        /// Tên là ChangeCount chứ không WriteCount đúng vì lý do đó: xoá cache và quét entry hết
        /// hạn cũng phải làm service flush thức dậy. Dùng WriteCount thì một lệnh purge sẽ không
        /// kích hoạt flush, và câu trả lời vừa xoá SỐNG LẠI từ file sau lần khởi động kế tiếp.
        /// </para>
        /// </summary>
        long ChangeCount { get; }

        /// <summary>
        /// Chụp lại tối đa <paramref name="maxEntries"/> entry để ghi xuống đĩa.
        /// KHÔNG giữ khóa trong lúc ghi đĩa: hàm này sao chép danh sách rồi nhả khóa ngay, phần
        /// chạm đĩa nằm ở <see cref="IAnswerCacheStore.SaveAsync"/>.
        /// </summary>
        AnswerCacheSnapshot ExportSnapshot(int maxEntries);

        /// <summary>
        /// Nạp ảnh chụp vào cache. TRỘN chứ không thay thế: entry đang sống trong RAM luôn thắng
        /// entry trên đĩa, vì service nạp chạy sau khi host đã bắt đầu nhận request. Trả về số
        /// entry thực sự nạp được.
        /// </summary>
        int ImportSnapshot(AnswerCacheSnapshot snapshot);

        /// <summary>
        /// Xoá entry đã hết hạn và ép trần số entry. Trả về tổng số entry đã xoá.
        /// Chạy ở nhịp nền chứ không trên đường nóng — lý do ở phần cài đặt.
        /// </summary>
        int SweepExpired(DateTime utcNow);
    }
}
