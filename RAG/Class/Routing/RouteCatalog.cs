using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>Một route đã sẵn sàng chấm điểm: template prompt cộng toàn bộ vector câu mẫu.</summary>
    public sealed record RouteVectors(
        string Name,
        double Threshold,
        string SystemPromptTemplate,
        string UserPromptTemplate,
        IReadOnlyList<float[]> Vectors);

    /// <summary>
    /// Trạng thái dùng chung của node định tuyến: bảng route đang phục vụ, vector của câu mẫu khai
    /// trong cấu hình, và câu mẫu thêm lúc chạy.
    /// <para>
    /// Tách ra thành một lớp riêng vì cả ba lớp cộng tác — bộ nạp (<see cref="RouteCatalogBuilder"/>),
    /// bộ quản trị câu mẫu (<see cref="RouteUtteranceAdmin"/>) và bản thân router — đều phải đọc
    /// và ghi đúng MỘT bản trạng thái này. Để mỗi lớp giữ một bản riêng là cách chắc chắn nhất để
    /// warm-up làm ấm một bảng mà router không hề dùng.
    /// </para>
    /// <para>
    /// Đường ĐỌC hoàn toàn không khóa: mọi trường đều <c>volatile</c> và chỉ được thay bằng một
    /// phép gán tham chiếu duy nhất (copy-on-write). Nhờ vậy request đang chạy không bao giờ nhìn
    /// thấy trạng thái nửa vời.
    /// </para>
    /// </summary>
    public sealed class RouteCatalog
    {
        private readonly SemanticRouterConfig _config;
        private readonly PromptConfig _promptConfig;
        private readonly ILogger<RouteCatalog> _logger;

        /// <summary><c>null</c> nghĩa là chưa nạp xong — mọi request đi đường RAG.</summary>
        private volatile IReadOnlyList<RouteVectors>? _routes;

        /// <summary>Vector của câu mẫu khai trong cấu hình; giữ lại để dựng lại bảng khi có câu mới.</summary>
        private volatile IReadOnlyDictionary<string, float[]>? _configVectors;

        /// <summary>Câu mẫu thêm lúc chạy; giữ lại để ghi đè toàn bộ file khi có thay đổi.</summary>
        private volatile IReadOnlyList<StoredUtterance> _addedUtterances = Array.Empty<StoredUtterance>();

        public RouteCatalog(IOptions<SemanticRouterConfig> options,
                            IOptions<PromptConfig> promptOptions,
                            ILogger<RouteCatalog> logger)
        {
            _config = options.Value;
            _promptConfig = promptOptions.Value;
            _logger = logger;
        }

        /// <summary>Bảng route đang phục vụ; <c>null</c> khi chưa nạp xong.</summary>
        public IReadOnlyList<RouteVectors>? Routes => _routes;

        public IReadOnlyList<StoredUtterance> AddedUtterances => _addedUtterances;

        public void SetConfigVectors(IReadOnlyDictionary<string, float[]> vectors) => _configVectors = vectors;

        public void SetAddedUtterances(IReadOnlyList<StoredUtterance> utterances) => _addedUtterances = utterances;

        /// <summary>Câu mẫu khai trong cấu hình, đã bỏ trùng và bỏ rỗng.</summary>
        public List<string> CollectConfiguredUtterances() =>
            _config.Routes
                .SelectMany(route => route.Utterances)
                .Where(utterance => !string.IsNullOrWhiteSpace(utterance))
                .Distinct(StringComparer.Ordinal)
                .ToList();

        public int CountUtterances(string routeName) =>
            _routes?.FirstOrDefault(route => string.Equals(route.Name, routeName, StringComparison.OrdinalIgnoreCase))
                ?.Vectors.Count ?? 0;

        /// <summary>
        /// Dựng lại bảng route từ vector cấu hình cộng câu mẫu thêm lúc chạy, rồi gán MỘT lần
        /// vào trường <c>_routes</c>. Trả về số route dựng được.
        /// </summary>
        public int Rebuild(int dims)
        {
            var configVectors = _configVectors ?? new Dictionary<string, float[]>(StringComparer.Ordinal);

            var addedByRoute = _addedUtterances
                .GroupBy(entry => entry.Route, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            // Luật "route nào dùng được và prompt của nó là gì" nằm ở RouteTableFactory, dùng chung
            // với chiến lược định tuyến bằng LLM. Ở đây chỉ còn đúng phần riêng: gắn vector vào.
            var resolved = RouteTableFactory.Resolve(_config, _promptConfig, _logger);
            var built = new List<RouteVectors>(resolved.Count);

            foreach (var route in resolved)
            {
                var routeVectors = route.Utterances
                    .Select(utterance => configVectors.TryGetValue(utterance, out var vector) ? vector : null)
                    .Where(vector => IsUsable(vector, dims))
                    .Select(vector => vector!)
                    .ToList();

                if (addedByRoute.TryGetValue(route.Name, out var added))
                    routeVectors.AddRange(added.Select(entry => entry.Vector).Where(vector => IsUsable(vector, dims)));

                if (routeVectors.Count == 0)
                {
                    _logger.LogWarning("Route {Route} không nạp được câu mẫu nào, bỏ qua.", route.Name);
                    continue;
                }

                built.Add(new RouteVectors(
                    route.Name,
                    route.Threshold,
                    route.SystemPromptTemplate,
                    route.UserPromptTemplate,
                    routeVectors));
            }

            _routes = built;
            return built.Count;
        }

        /// <summary>
        /// Nhà cung cấp embedding có thể trả vector rác, nên vector phải bị chặn ở đây — nếu không
        /// nó sẽ được ghi vào cache và tồn tại qua mọi lần khởi động.
        /// </summary>
        public static bool IsUsable(float[]? vector, int dims) =>
            vector is not null && vector.Length == dims && VectorMath.HasMagnitude(vector);
    }
}
