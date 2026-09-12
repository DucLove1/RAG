using System.Diagnostics;
using RAG.Interface;

namespace RAG.Class.Diagnostics
{
    /// <summary>
    /// Sổ ghi của MỘT lượt đo. Sống đúng bằng một request và không được chia sẻ giữa các request —
    /// việc giữ đúng một phiên cho mỗi luồng logic là việc của <see cref="AsyncLocalLatencyTracker"/>.
    /// </summary>
    internal sealed class LatencySession
    {
        /// <summary>
        /// Mốc thời gian bắt đầu, dạng tick của <see cref="Stopwatch"/> chứ không phải một object
        /// <c>Stopwatch</c>: một long trên stack thay vì một lần cấp phát heap cho mỗi request.
        /// </summary>
        private readonly long _startedAt = Stopwatch.GetTimestamp();

        /// <summary>
        /// Giữ thứ tự gặp đầu tiên để dòng log đọc được theo đúng trình tự pipeline chạy. Một
        /// <c>Dictionary</c> thường KHÔNG bảo đảm điều đó, nên thứ tự được giữ riêng ở
        /// <see cref="_order"/>.
        /// </summary>
        private readonly Dictionary<string, StageAccumulator> _stages = new(StringComparer.Ordinal);
        private readonly List<string> _order = new();
        // Nhãn cũng giữ thứ tự gặp đầu tiên, cùng lý do với stage: thứ tự duyệt của Dictionary là
        // chi tiết cài đặt, và một dòng log đổi thứ tự cột giữa hai lần chạy thì không grep được.
        private readonly Dictionary<string, string> _tags = new(StringComparer.Ordinal);
        private readonly List<string> _tagOrder = new();

        /// <summary>
        /// Khóa vì một phiên có thể bị ghi từ nhiều luồng: <c>AsyncLocal</c> chảy sang mọi nhánh
        /// <c>Task</c> con, nên hai lời gọi chạy song song trong cùng request sẽ cùng ghi vào đây.
        /// Hiện AskPipeline chạy tuần tự, nhưng khóa này là thứ giữ cho việc song song hóa về sau
        /// không âm thầm làm hỏng số liệu.
        /// </summary>
        private readonly object _gate = new();

        public LatencySession(string operation) => Operation = operation;

        public string Operation { get; }

        public void Record(string stage, double milliseconds)
        {
            lock (_gate)
            {
                if (_stages.TryGetValue(stage, out var accumulator))
                {
                    accumulator.Add(milliseconds);
                    return;
                }

                _stages[stage] = new StageAccumulator(milliseconds);
                _order.Add(stage);
            }
        }

        public void Tag(string name, string value)
        {
            lock (_gate)
            {
                if (_tags.TryAdd(name, value))
                    _tagOrder.Add(name);
                else
                    _tags[name] = value;
            }
        }

        /// <summary>Đọc một nhãn đã gắn. <c>TimedAskService</c> cần nó để suy ra nhánh lúc chốt sổ.</summary>
        public string? GetTag(string name)
        {
            lock (_gate)
                return _tags.GetValueOrDefault(name);
        }

        /// <summary>
        /// Chốt sổ. Tổng được đo bằng đồng hồ tường chứ không phải tổng các stage — xem chú thích
        /// của <see cref="LatencyReport.TotalMilliseconds"/>.
        /// </summary>
        public LatencyReport Build()
        {
            var total = Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds;

            lock (_gate)
            {
                var stages = new List<LatencyStageSample>(_order.Count);

                foreach (var stage in _order)
                {
                    var accumulator = _stages[stage];
                    stages.Add(new LatencyStageSample(stage, accumulator.Milliseconds, accumulator.Calls));
                }

                // Dictionary thường mới là kiểu giữ được thứ tự chèn khi duyệt ở đây, vì nó được
                // dựng lại theo _tagOrder và không có lần xoá nào.
                var tags = new Dictionary<string, string>(_tagOrder.Count, StringComparer.Ordinal);

                foreach (var name in _tagOrder)
                    tags[name] = _tags[name];

                return new LatencyReport(Operation, total, stages, tags);
            }
        }

        /// <summary>
        /// Cộng dồn cho một stage. Là class chứ không phải struct để cập nhật tại chỗ trong
        /// <c>Dictionary</c> mà không phải gán lại cả entry.
        /// </summary>
        private sealed class StageAccumulator
        {
            public StageAccumulator(double milliseconds)
            {
                Milliseconds = milliseconds;
                Calls = 1;
            }

            public double Milliseconds { get; private set; }

            public int Calls { get; private set; }

            public void Add(double milliseconds)
            {
                Milliseconds += milliseconds;
                Calls++;
            }
        }
    }
}
