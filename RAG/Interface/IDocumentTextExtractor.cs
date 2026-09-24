namespace RAG.Interface
{
    /// <summary>
    /// Nội dung rút ra từ một file: phần thân và phần siêu dữ liệu đi kèm.
    /// <para>
    /// Bản trước chỉ trả <c>string</c>, nên siêu dữ liệu không có đường nào đi qua. Với corpus có
    /// YAML front matter thì đó là một mất mát thật: mã tài liệu, loại, màn chơi và DANH SÁCH NPC
    /// ĐƯỢC BIẾT đều nằm trong khối đó, và nếu không bóc ra thì chính mấy dòng front matter lại
    /// biến thành đoạn văn bản rác được đem đi nhúng.
    /// </para>
    /// </summary>
    /// <param name="Metadata">Rỗng với những định dạng không mang siêu dữ liệu, không bao giờ null.</param>
    public sealed record ExtractedDocument(string Body, IReadOnlyDictionary<string, string> Metadata)
    {
        public static ExtractedDocument PlainBody(string body) =>
            new(body, new Dictionary<string, string>());
    }

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
    /// <para>
    /// Hai cài đặt KHÔNG được nhận cùng một phần mở rộng: bộ nạp chọn cài đặt ĐẦU TIÊN nhận, nên
    /// chồng lấn sẽ làm kết quả phụ thuộc vào thứ tự đăng ký. Danh sách nằm trong cấu hình chính là
    /// nơi giữ cho chúng rời nhau.
    /// </para>
    /// </summary>
    public interface IDocumentTextExtractor
    {
        /// <param name="extension">Phần mở rộng đã chuẩn hóa chữ thường, kèm dấu chấm (ví dụ ".txt").</param>
        bool Supports(string extension);

        Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken cancellationToken = default);
    }
}
