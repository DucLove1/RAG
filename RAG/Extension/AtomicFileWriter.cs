namespace RAG.Extension
{
    /// <summary>
    /// Ghi file theo kiểu nguyên tử: ghi ra file tạm rồi đổi tên đè lên file thật.
    /// <para>
    /// Tiến trình chết giữa chừng thì file cũ vẫn nguyên vẹn, thay vì để lại một file cụt mà lần
    /// khởi động sau đọc vào sẽ hỏng. Với container bị dừng bằng SIGKILL thì đây không phải trường
    /// hợp hiếm.
    /// </para>
    /// <para>
    /// Gom về một chỗ vì ba kho file (cache hỏi đáp, cache vector route, kho câu mẫu) đều lặp lại
    /// đúng bốn bước này. Lặp ba lần nghĩa là sửa sót một chỗ sẽ không ai phát hiện.
    /// </para>
    /// </summary>
    public static class AtomicFileWriter
    {
        private const string TemporarySuffix = ".tmp";

        /// <summary>Bản bất đồng bộ, cho nội dung ghi bằng API async (ví dụ JsonSerializer).</summary>
        public static async Task WriteAsync(string path, Func<Stream, Task> writeContent)
        {
            var temporaryPath = PrepareTemporaryPath(path);

            await using (var stream = File.Create(temporaryPath))
            {
                await writeContent(stream);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }

        /// <summary>Bản đồng bộ, cho nội dung ghi bằng API đồng bộ (ví dụ BinaryWriter).</summary>
        public static void Write(string path, Action<Stream> writeContent)
        {
            var temporaryPath = PrepareTemporaryPath(path);

            using (var stream = File.Create(temporaryPath))
            {
                writeContent(stream);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }

        private static string PrepareTemporaryPath(string path)
        {
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            return path + TemporarySuffix;
        }
    }
}
