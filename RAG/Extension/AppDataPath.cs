namespace RAG.Extension
{
    /// <summary>
    /// Giải đường dẫn tới các file dữ liệu do ứng dụng sinh ra (cache vector, kho câu mẫu).
    /// </summary>
    public static class AppDataPath
    {
        /// <summary>
        /// Đường dẫn tuyệt đối giữ nguyên; đường dẫn tương đối giải theo <c>ContentRootPath</c>
        /// chứ KHÔNG theo <c>AppContext.BaseDirectory</c>.
        /// <para>
        /// Lý do: <c>BaseDirectory</c> là thư mục build output, nên "App_Data/x.json" trong config
        /// thực chất rơi vào <c>bin\Debug\net9.0\App_Data\</c> — người đọc config không đoán ra,
        /// và <c>dotnet clean</c> hay đổi Debug↔Release là mất sạch cache.
        /// </para>
        /// <para>
        /// Trong container hai giá trị này trùng nhau (đều là <c>/app</c>) nên thay đổi này không
        /// ảnh hưởng Docker; nó chỉ làm môi trường dev dễ hiểu hơn.
        /// </para>
        /// </summary>
        public static string Resolve(IHostEnvironment environment, string configuredPath) =>
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);
    }
}
