using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using System.Text;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Đọc file Markdown có khối YAML front matter: bóc siêu dữ liệu ra khỏi phần thân.
    /// <para>
    /// Không bóc thì mấy dòng <c>doc_id: "..."</c> sẽ được bộ cắt theo dòng đếm như tri thức thật,
    /// chiếm mất các mã <c>#L1</c>..<c>#L7</c> và đẩy lệch TOÀN BỘ phần còn lại của tài liệu. Đó là
    /// lý do lớp này tồn tại thay vì để <see cref="PlainTextExtractor"/> nhận luôn <c>.md</c>.
    /// </para>
    /// <para>
    /// Tự phân tích thay vì kéo thêm một package YAML: front matter của corpus này là bảy khóa
    /// phẳng với ba dạng giá trị (<c>"chuỗi"</c>, <c>[a, b]</c>, trần), nên một bộ đọc vài chục dòng
    /// là đủ và đúng. Dùng một thư viện YAML đầy đủ ở đây là trả giá bằng một phụ thuộc mới cho
    /// những tính năng (anchor, khối nhiều dòng, kiểu lồng nhau) mà định dạng này không dùng tới.
    /// </para>
    /// </summary>
    public sealed class MarkdownFrontMatterExtractor : IDocumentTextExtractor
    {
        private readonly HashSet<string> _extensions;

        public MarkdownFrontMatterExtractor(IOptions<IngestionConfig> options)
        {
            _extensions = options.Value.FrontMatterExtensions
                .Select(extension => extension.Trim().ToLowerInvariant())
                .Where(extension => extension.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
        }

        public bool Supports(string extension) => _extensions.Contains(extension);

        public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var raw = await reader.ReadToEndAsync(cancellationToken);

            return Parse(raw);
        }

        /// <summary>
        /// Tách theo dấu ngăn <c>---</c>. Không có khối front matter thì coi cả file là phần thân —
        /// một file Markdown thường vẫn nạp được, chỉ là không có siêu dữ liệu.
        /// </summary>
        internal static ExtractedDocument Parse(string raw)
        {
            var normalized = raw.Replace("\r\n", "\n");
            var trimmed = normalized.TrimStart();

            if (!trimmed.StartsWith(FrontMatterFields.Delimiter, StringComparison.Ordinal))
                return ExtractedDocument.PlainBody(normalized);

            var afterOpening = trimmed[FrontMatterFields.Delimiter.Length..];

            // Tìm dấu ngăn ĐÓNG ở đầu một dòng. Tìm chuỗi "---" ở bất kỳ đâu sẽ dính phải một dấu
            // gạch ngang nằm giữa giá trị.
            var closing = afterOpening.IndexOf("\n" + FrontMatterFields.Delimiter, StringComparison.Ordinal);

            if (closing < 0)
                return ExtractedDocument.PlainBody(normalized);

            var block = afterOpening[..closing];
            var body = afterOpening[(closing + 1 + FrontMatterFields.Delimiter.Length)..];

            return new ExtractedDocument(body, ParseBlock(block));
        }

        private static Dictionary<string, string> ParseBlock(string block)
        {
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var line in block.Split('\n'))
            {
                var separator = line.IndexOf(':');

                // Dòng không có dấu hai chấm không phải một cặp khóa-giá trị. Bỏ qua thay vì ném:
                // một dòng trống hay một dòng chú thích không làm cả tài liệu thành không hợp lệ.
                if (separator <= 0)
                    continue;

                var key = line[..separator].Trim();
                var value = Unwrap(line[(separator + 1)..].Trim());

                if (key.Length > 0)
                    metadata[key] = value;
            }

            return metadata;
        }

        /// <summary>
        /// Gỡ dấu nháy bao quanh và dấu ngoặc vuông của danh sách. Danh sách giữ nguyên dạng chuỗi
        /// ngăn bằng dấu phẩy — payload của kho vector là kiểu phẳng, và mọi thứ đọc trường này đều
        /// tách lại bằng dấu phẩy.
        /// </summary>
        private static string Unwrap(string value)
        {
            if (value.Length >= 2 && value[0] == '[' && value[^1] == ']')
                value = value[1..^1];

            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];

            return value.Trim();
        }
    }
}
