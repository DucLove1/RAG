namespace RAG.Interface
{
    /// <summary>
    /// Một tài liệu được tải lên, ở dạng trung lập với web framework.
    /// <para>
    /// Cố tình KHÔNG dùng <c>IFormFile</c>: đó là kiểu của ASP.NET, và để nó đi vào tầng nghiệp vụ
    /// nghĩa là đường nạp dữ liệu chỉ chạy được khi có một HTTP request. Nạp từ dòng lệnh hay từ
    /// một job nền sẽ không dùng lại được gì.
    /// </para>
    /// </summary>
    /// <param name="FileName">Tên file gốc; dùng để suy ra định dạng và để truy vết.</param>
    /// <param name="Content">Luồng nội dung. Người gọi chịu trách nhiệm đóng nó.</param>
    public sealed record DocumentSource(string FileName, Stream Content);

    /// <summary>
    /// Kết quả một lần nạp. Trả về số liệu thay vì chỉ 200/400 để người gọi biết file nào bị bỏ.
    /// </summary>
    /// <param name="FilesProcessed">Số file rút được nội dung.</param>
    /// <param name="FilesSkipped">Số file bị bỏ vì không hỗ trợ định dạng hoặc rỗng.</param>
    /// <param name="ChunksIngested">Số đoạn đã cắt và đưa đi nhúng.</param>
    public sealed record IngestionResult(int FilesProcessed, int FilesSkipped, int ChunksIngested);
}
