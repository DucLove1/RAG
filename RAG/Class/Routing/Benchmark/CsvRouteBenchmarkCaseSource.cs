using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Extension;
using RAG.Interface;
using System.Text;

namespace RAG.Class.Routing.Benchmark
{
    /// <summary>
    /// Đọc bộ câu hỏi từ CSV (UTF-8, có hoặc không BOM; ô có dấu phẩy bọc trong ngoặc kép).
    /// Đọc lại ở MỖI lần chạy, không giữ bản trong bộ nhớ.
    /// </summary>
    public sealed class CsvRouteBenchmarkCaseSource : IRouteBenchmarkCaseSource
    {
        private readonly RouteBenchmarkConfig _config;

        public CsvRouteBenchmarkCaseSource(IOptions<RouteBenchmarkConfig> options)
        {
            _config = options.Value;
        }

        public IReadOnlyList<RouteBenchmarkCase> Load()
        {
            using var parser = new TextFieldParser(ContentFilePath.Resolve(_config.CasesPath), Encoding.UTF8)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(RouteBenchmarkCsvColumns.Delimiter);

            var header = parser.ReadFields() ?? Array.Empty<string>();
            var index = header
                .Select((name, position) => (name, position))
                .ToDictionary(column => column.name, column => column.position, StringComparer.OrdinalIgnoreCase);

            var cases = new List<RouteBenchmarkCase>();

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields is null)
                    continue;

                string Field(string column) =>
                    index.TryGetValue(column, out var position) && position < fields.Length
                        ? fields[position]
                        : string.Empty;

                var question = Field(RouteBenchmarkCsvColumns.Question);
                if (string.IsNullOrWhiteSpace(question))
                    continue;

                cases.Add(new RouteBenchmarkCase(
                    Field(RouteBenchmarkCsvColumns.Id),
                    question,
                    Field(RouteBenchmarkCsvColumns.ExpectedRoute),
                    Field(RouteBenchmarkCsvColumns.Tag),
                    Field(RouteBenchmarkCsvColumns.Note)));
            }

            return cases;
        }
    }
}
