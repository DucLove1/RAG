using RAG.Class.Constants;
using System.Globalization;
using System.Text;

namespace RAG.Class.Routing
{
    /// <summary>Kết cục của một lần đọc nhãn từ đầu ra của LLM.</summary>
    public enum RouteLabelOutcome
    {
        /// <summary>Đọc ra đúng một tên route đã khai báo.</summary>
        Matched = 0,

        /// <summary>LLM đã QUYẾT ĐỊNH không route nào khớp (xuất đúng nhãn không khớp).</summary>
        NoMatch = 1,

        /// <summary>Không đọc được nhãn nào. Cũng đi đường RAG, nhưng vì fail-open chứ không phải quyết định.</summary>
        Unparseable = 2
    }

    /// <param name="RouteName">Tên route đúng như khai trong cấu hình; rỗng nếu không phải <see cref="RouteLabelOutcome.Matched"/>.</param>
    public sealed record RouteLabelResolution(RouteLabelOutcome Outcome, string RouteName);

    /// <summary>
    /// Đọc tên route ra khỏi đầu ra của LLM.
    /// <para>
    /// Tách khỏi router vì đây là việc thuần cú pháp, không cần biết gì về định tuyến hay cấu hình
    /// prompt — và vì nó là phần duy nhất của node có thể kiểm chứng bằng cách nhìn vào đầu vào và
    /// đầu ra, không cần gọi mạng.
    /// </para>
    /// <para>
    /// Prompt đã yêu cầu mô hình chỉ xuất một nhãn trần, nhưng bộ phân tích vẫn phải chịu được
    /// những kiểu "nói thêm" thường gặp: bọc code fence, thêm tiền tố "Nhãn:", in đậm bằng
    /// markdown, viết hoa, bỏ dấu, giải thích một đoạn rồi mới trả lời. Một prompt tốt làm những
    /// chuyện đó hiếm đi chứ không làm chúng biến mất.
    /// </para>
    /// </summary>
    public sealed class RouteLabelParser
    {
        /// <summary>Tên route đã chuẩn hóa → tên đúng như khai trong cấu hình.</summary>
        private readonly IReadOnlyDictionary<string, string> _routeNames;

        private readonly string _noMatchLabel;

        public RouteLabelParser(IEnumerable<string> routeNames, string noMatchLabel)
        {
            _routeNames = routeNames
                .Select(name => new { Normalized = Normalize(name), Original = name })
                .Where(entry => entry.Normalized.Length > 0)
                .GroupBy(entry => entry.Normalized, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Original, StringComparer.Ordinal);

            _noMatchLabel = Normalize(noMatchLabel);
        }

        public RouteLabelResolution Resolve(string? output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return new RouteLabelResolution(RouteLabelOutcome.Unparseable, string.Empty);

            var lines = StripCodeFence(output)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            // Duyệt từ dưới lên: mô hình nói nhiều thì kết luận nằm ở dòng cuối, còn phần dẫn dắt
            // ở trên có thể nhắc tên vài route mà nó đã cân nhắc rồi loại.
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var candidate = Normalize(TrimDecoration(lines[i]));

                if (candidate.Length == 0)
                    continue;

                if (candidate == _noMatchLabel)
                    return new RouteLabelResolution(RouteLabelOutcome.NoMatch, string.Empty);

                if (_routeNames.TryGetValue(candidate, out var routeName))
                    return new RouteLabelResolution(RouteLabelOutcome.Matched, routeName);
            }

            return ScanWholeOutput(output);
        }

        /// <summary>
        /// Cứu vãn cuối cùng: quét toàn bộ đầu ra tìm tên route dạng token nguyên.
        /// <para>
        /// Chỉ nhận khi tìm thấy ĐÚNG MỘT tên. Thấy hai tên trở lên nghĩa là mô hình đang liệt kê
        /// hoặc đang so sánh các lựa chọn, và đoán bừa lấy một cái là cách chắc chắn nhất để định
        /// tuyến sai một cách khó lần ra — đi đường RAG an toàn hơn nhiều.
        /// </para>
        /// </summary>
        private RouteLabelResolution ScanWholeOutput(string output)
        {
            var normalized = Normalize(output);

            if (normalized.Length == 0)
                return new RouteLabelResolution(RouteLabelOutcome.Unparseable, string.Empty);

            var tokens = normalized
                .Split(RouteLabelSyntax.WordSeparator, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);

            // Tên route có thể gồm nhiều từ sau khi chuẩn hóa ("out_of_scope"), nên so cả chuỗi con
            // lẫn token đơn: một tên khớp khi mọi phần của nó đều xuất hiện.
            var found = _routeNames
                .Where(entry => normalized.Contains(entry.Key, StringComparison.Ordinal) ||
                                entry.Key.Split(RouteLabelSyntax.WordSeparator, StringSplitOptions.RemoveEmptyEntries)
                                         .All(tokens.Contains))
                .Select(entry => entry.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return found.Count == 1
                ? new RouteLabelResolution(RouteLabelOutcome.Matched, found[0])
                : new RouteLabelResolution(RouteLabelOutcome.Unparseable, string.Empty);
        }

        /// <summary>Bóc khối code mà mô hình hay bọc quanh câu trả lời.</summary>
        private static string StripCodeFence(string output)
        {
            var text = output.Replace("\r\n", "\n").Trim();

            if (!text.StartsWith(RouteLabelSyntax.CodeFence, StringComparison.Ordinal))
                return text;

            text = text[RouteLabelSyntax.CodeFence.Length..];

            var closing = text.IndexOf(RouteLabelSyntax.CodeFence, StringComparison.Ordinal);

            if (closing >= 0)
                text = text[..closing];

            // Bỏ info string ("```json") nếu nó chiếm trọn dòng đầu.
            var newline = text.IndexOf('\n');
            var firstLine = (newline >= 0 ? text[..newline] : text).Trim();

            if (newline >= 0 && RouteLabelSyntax.CodeFenceInfoStrings.Contains(firstLine, StringComparer.OrdinalIgnoreCase))
                text = text[(newline + 1)..];

            return text.Trim();
        }

        /// <summary>Cắt tiền tố ("Nhãn:", "- ") và gỡ dấu nháy, dấu nhấn markdown, dấu câu cuối.</summary>
        private static string TrimDecoration(string line)
        {
            // Cắt tại dấu ngăn CUỐI CÙNG: tên route không chứa chúng, nên mọi thứ phía trước
            // chắc chắn là tiền tố dẫn dắt.
            var separator = line.LastIndexOfAny(RouteLabelSyntax.LabelSeparators);

            if (separator >= 0 && separator < line.Length - 1)
                line = line[(separator + 1)..];

            return line.Trim()
                       .Trim(RouteLabelSyntax.Quotes)
                       .TrimEnd(RouteLabelSyntax.TrailingPunctuation)
                       .Trim();
        }

        /// <summary>
        /// Quy mọi biến thể viết của một nhãn về cùng một khóa: thường hóa, bỏ dấu tiếng Việt,
        /// và gộp mọi dấu ngăn từ về dấu gạch dưới. Nhờ vậy "Chitchat", "CHIT CHAT", "chit-chat"
        /// và "out of scope" đều tra được.
        /// <para>
        /// Cũng là hàm dựng khóa cho từ điển, nên đầu vào và đầu ra luôn đi qua đúng một phép biến
        /// đổi — không có cách nào để hai bên lệch nhau.
        /// </para>
        /// </summary>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            var lastWasSeparator = true;

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (character == 'đ')
                {
                    builder.Append('d');
                    lastWasSeparator = false;
                    continue;
                }

                if (RouteLabelSyntax.WordSeparators.Contains(character) || character == RouteLabelSyntax.WordSeparator)
                {
                    // Gộp dấu ngăn liên tiếp để "chit -- chat" và "chit_chat" ra cùng một khóa.
                    if (!lastWasSeparator)
                    {
                        builder.Append(RouteLabelSyntax.WordSeparator);
                        lastWasSeparator = true;
                    }

                    continue;
                }

                if (!char.IsLetterOrDigit(character))
                    continue;

                builder.Append(character);
                lastWasSeparator = false;
            }

            return builder.ToString().Trim(RouteLabelSyntax.WordSeparator).Normalize(NormalizationForm.FormC);
        }
    }
}
