using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;
using System.Text;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Đọc các định dạng vốn đã là văn bản thuần (.txt, .md, .json).
    /// Danh sách phần mở rộng nằm trong cấu hình chứ không trong code.
    /// </summary>
    public sealed class PlainTextExtractor : IDocumentTextExtractor
    {
        private readonly HashSet<string> _extensions;

        public PlainTextExtractor(IOptions<IngestionConfig> options)
        {
            _extensions = options.Value.PlainTextExtensions
                .Select(extension => extension.Trim().ToLowerInvariant())
                .Where(extension => extension.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
        }

        public bool Supports(string extension) => _extensions.Contains(extension);

        public async Task<string> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
        {
            // detectEncodingFromByteOrderMarks để đọc được cả file có BOM; Program.cs đã đăng ký
            // CodePagesEncodingProvider cho các file mã hóa ANSI/Windows cũ.
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
