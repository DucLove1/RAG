namespace RAG.Interface
{
    /// <summary>
    /// Rút văn bản thuần từ một file tải lên.
    /// <para>
    /// Mỗi định dạng là một cài đặt riêng, và danh sách định dạng được hỗ trợ chính là tập hợp
    /// các cài đặt đang đăng ký. Nhờ vậy thêm PDF về sau chỉ là thêm một lớp — không phải sửa
    /// controller hay bất kỳ danh sách phần mở rộng nào (OCP).
    /// </para>
    /// <para>
    /// Bản trước để whitelist ".pdf/.txt/.md/.json" cứng trong controller, trong đó ".pdf" được
    /// cho qua nhưng không có nhánh nào đọc nó — file PDF bị bỏ im lặng và người dùng chỉ nhận
    /// được "No valid files uploaded" mà không hiểu vì sao.
    /// </para>
    /// </summary>
    public interface IDocumentTextExtractor
    {
        /// <param name="extension">Phần mở rộng đã chuẩn hóa chữ thường, kèm dấu chấm (ví dụ ".txt").</param>
        bool Supports(string extension);

        Task<string> ExtractAsync(Stream content, CancellationToken cancellationToken = default);
    }
}
