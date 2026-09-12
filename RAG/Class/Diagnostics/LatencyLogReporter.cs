using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Diagnostics
{
    /// <summary>
    /// Xuất báo cáo ra <see cref="ILogger"/> dưới dạng MỘT dòng cho mỗi request.
    /// <para>
    /// Một dòng chứ không phải một dòng cho mỗi stage: tám dòng rời rạc của hai request chạy song
    /// song sẽ trộn lẫn vào nhau trong console và không còn cách nào ghép lại đúng request nào.
    /// </para>
    /// </summary>
    public sealed class LatencyLogReporter : ILatencyReporter
    {
        private readonly ILogger<LatencyLogReporter> _logger;
        private readonly LatencyConfig _config;

        public LatencyLogReporter(ILogger<LatencyLogReporter> logger, IOptions<LatencyConfig> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public void Report(LatencyReport report)
        {
            // Vượt ngưỡng thì lên Warning: người vận hành lọc theo mức log, và một request 30 giây
            // lẫn giữa hàng nghìn dòng Information thì cũng như không có.
            var level = report.TotalMilliseconds > _config.SlowThresholdMs
                ? LogLevel.Warning
                : LogLevel.Information;

            // Thoát sớm TRƯỚC khi dựng chuỗi: mức log này thường bị tắt ở production, và dựng chuỗi
            // rồi vứt đi cho mỗi request là chi phí duy nhất mà tầng đo giờ này thực sự phải trả.
            if (!_logger.IsEnabled(level))
                return;

            _logger.Log(level,
                _config.MessageTemplate,
                report.Operation,
                report.TotalMilliseconds.ToString(_config.MillisecondFormat, CultureInfo.InvariantCulture),
                FormatTags(report.Tags),
                FormatStages(report.Stages));
        }

        private string FormatStages(IReadOnlyList<LatencyStageSample> stages)
        {
            var builder = new StringBuilder();

            foreach (var stage in stages)
            {
                if (builder.Length > 0)
                    builder.Append(_config.StageSeparator);

                var milliseconds = stage.Milliseconds.ToString(_config.MillisecondFormat, CultureInfo.InvariantCulture);

                // Chỉ in số lần gọi khi nó khác 1: thêm "x1" vào mọi stage làm dòng log dài thêm mà
                // không nói được gì, còn "x2" thì đáng để mắt tới.
                builder.AppendFormat(CultureInfo.InvariantCulture,
                    stage.Calls > 1 ? _config.StageWithCallsFormat : _config.StageFormat,
                    stage.Stage,
                    milliseconds,
                    stage.Calls);
            }

            return builder.ToString();
        }

        private string FormatTags(IReadOnlyDictionary<string, string> tags)
        {
            var builder = new StringBuilder();

            foreach (var tag in tags)
            {
                if (builder.Length > 0)
                    builder.Append(_config.TagSeparator);

                builder.AppendFormat(CultureInfo.InvariantCulture, _config.TagFormat, tag.Key, tag.Value);
            }

            return builder.ToString();
        }
    }
}
