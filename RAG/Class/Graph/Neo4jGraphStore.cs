using Microsoft.Extensions.Options;
using Neo4j.Driver;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Cài đặt <see cref="IGraphStore"/> trên Neo4j.
    /// <para>
    /// Cùng với composition root và bộ nạp, đây là một trong ba file duy nhất import
    /// <c>Neo4j.Driver</c>. <c>Interface/</c> không bao giờ thấy kiểu của driver — đó đúng là chiều
    /// DIP mà codebase đã sửa một lần với <c>IQdrantProvider</c>.
    /// </para>
    /// <para>
    /// BẤT BIẾN CỦA LỚP: hai method public KHÔNG BAO GIỜ ném, trừ khi chính token của caller bị hủy.
    /// Đồ thị chết phải là chuyện vô hình với người chơi — câu trả lời tụt về RAG thuần, không phải
    /// về 500. Mọi nhánh <c>catch</c> ở đây đều phục vụ bất biến đó.
    /// </para>
    /// <para>
    /// Lưu ý vận hành: Aura Free tự ngủ sau vài ngày không dùng, và truy vấn đầu tiên sau khi nó
    /// thức dậy có thể mất hàng chục giây — đủ để làm nhảy ngắt mạch. Đó là hành vi ĐÚNG, không
    /// phải bug: vài câu hỏi đầu trả lời bằng RAG thuần, rồi request thăm dò sau thời gian nghỉ sẽ
    /// đóng mạch lại.
    /// </para>
    /// </summary>
    public sealed class Neo4jGraphStore : IGraphStore, IGraphStatistics, IGraphWarmup
    {
        private readonly IDriver _driver;
        private readonly IOntology _ontology;
        private readonly Neo4jConfig _config;
        private readonly GraphCircuitBreaker _breaker;
        private readonly ILogger<Neo4jGraphStore> _logger;

        private readonly string _expandQuery;
        private readonly string _knownEntitiesQuery;

        /// <summary>Nhãn không mang thông tin loại cho LLM: nhãn nền có trên mọi node, nhãn NPC là phân quyền.</summary>
        private readonly HashSet<string> _hiddenLabels;

        private long _expansions;
        private long _emptyResults;
        private long _errors;

        public Neo4jGraphStore(IDriver driver,
                               IOntology ontology,
                               IOptions<Neo4jConfig> options,
                               ILogger<Neo4jGraphStore> logger)
        {
            _driver = driver;
            _ontology = ontology;
            _config = options.Value;
            _logger = logger;
            _breaker = new GraphCircuitBreaker(_config.MaxConsecutiveFailures, _config.FailureCooldownSeconds, logger);

            // Ghép nhãn vào câu lệnh MỘT lần lúc khởi động, không phải mỗi request: nhãn đến từ
            // ontology và không đổi trong một vòng đời tiến trình.
            _expandQuery = string.Format(GraphCypher.ExpandTemplate, _ontology.NpcLabel, _ontology.BaseLabel);
            _knownEntitiesQuery = string.Format(GraphCypher.KnownEntitiesTemplate, _ontology.NpcLabel, _ontology.BaseLabel);

            _hiddenLabels = new HashSet<string>(StringComparer.Ordinal) { _ontology.BaseLabel, _ontology.NpcLabel };
        }

        public GraphStats GetStats() => new(
            Interlocked.Read(ref _expansions),
            Interlocked.Read(ref _emptyResults),
            Interlocked.Read(ref _errors));

        /// <summary>
        /// Mở kết nối rồi chạy đúng hai câu Cypher của đường trả lời với một tên không tồn tại.
        /// <para>
        /// <c>VerifyConnectivityAsync</c> trả phần TLS và bảng định tuyến KHÔNG bị
        /// <c>OperationTimeoutMs</c> chặn, rồi lượt <see cref="ExpandAsync"/> và
        /// <see cref="ListKnownEntitiesAsync"/> nạp sẵn kế hoạch của chính các câu lệnh mà đường trả
        /// lời sẽ chạy — cùng chuỗi lệnh, chỉ khác tham số.
        /// </para>
        /// </summary>
        public async Task<bool> WarmUpAsync(CancellationToken cancellationToken = default)
        {
            await _driver.VerifyConnectivityAsync().WaitAsync(cancellationToken);

            // Danh sách hạt giống phải KHÁC RỖNG: rỗng thì ExpandAsync trả ngay mà không chạm Neo4j,
            // và lượt làm nóng thành vô nghĩa.
            var expansion = await ExpandAsync(
                new GraphExpansionRequest(WarmupName, new[] { WarmupName }, Array.Empty<string>(),
                                          ReasoningOnly: false, Limit: 1),
                cancellationToken);

            var catalog = await ListKnownEntitiesAsync(WarmupName, cancellationToken);

            return !expansion.Failed && !catalog.Failed;
        }

        /// <summary>Tên không bao giờ tồn tại: làm nóng chỉ cần câu lệnh chạy, không cần kết quả.</summary>
        private const string WarmupName = "__warmup__";

        public async Task<GraphStoreResult<GraphEdge>> ExpandAsync(GraphExpansionRequest request,
                                                                   CancellationToken cancellationToken = default)
        {
            // Không có hạt giống thì KHÔNG gọi Neo4j: một round-trip chắc chắn trả về rỗng vẫn là một
            // round-trip, và nó nằm trên đường nóng có người chơi đang chờ.
            if (request.EntityNames.Count == 0)
                return GraphStoreResult<GraphEdge>.None;

            if (_breaker.IsOpen())
                return GraphStoreResult<GraphEdge>.Unavailable;

            var parameters = new Dictionary<string, object>
            {
                [GraphCypher.Parameters.Npc] = request.NpcName,
                [GraphCypher.Parameters.Entities] = request.EntityNames.ToList(),
                [GraphCypher.Parameters.IntentTypes] = request.IntentRelationTypes.ToList(),
                [GraphCypher.Parameters.AllowedStatuses] = _ontology.AllowedStatuses.ToList(),
                [GraphCypher.Parameters.SymmetricTypes] = _ontology.SymmetricRelations.ToList(),
                [GraphCypher.Parameters.ReasoningTypes] = _ontology.ReasoningRelations.ToList(),
                [GraphCypher.Parameters.StatusPriority] = _ontology.StatusPriority.ToDictionary(
                    entry => entry.Key, entry => (object)entry.Value),
                [GraphCypher.Parameters.ReasoningOnly] = request.ReasoningOnly,
                [GraphCypher.Parameters.Limit] = request.Limit
            };

            var records = await RunAsync(_expandQuery, parameters, cancellationToken);

            if (records is null)
                return GraphStoreResult<GraphEdge>.Unavailable;

            Interlocked.Increment(ref _expansions);

            if (records.Count == 0)
                Interlocked.Increment(ref _emptyResults);

            return GraphStoreResult<GraphEdge>.Ok(records.Select(ToEdge).ToList());
        }

        public async Task<GraphStoreResult<GraphCatalogEntry>> ListKnownEntitiesAsync(string npcName,
                                                                                      CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return GraphStoreResult<GraphCatalogEntry>.None;

            if (_breaker.IsOpen())
                return GraphStoreResult<GraphCatalogEntry>.Unavailable;

            var records = await RunAsync(_knownEntitiesQuery, new Dictionary<string, object>
            {
                [GraphCypher.Parameters.Npc] = npcName
            }, cancellationToken);

            if (records is null)
                return GraphStoreResult<GraphCatalogEntry>.Unavailable;

            return GraphStoreResult<GraphCatalogEntry>.Ok(records.Select(ToCatalogEntry).ToList());
        }

        /// <summary>
        /// Chạy một truy vấn CHỈ ĐỌC và nuốt mọi lỗi.
        /// </summary>
        /// <returns><c>null</c> nghĩa là truy vấn hỏng; danh sách rỗng nghĩa là chạy xong và không có gì.</returns>
        private async Task<List<IRecord>?> RunAsync(string query,
                                                    Dictionary<string, object> parameters,
                                                    CancellationToken cancellationToken)
        {
            try
            {
                // Session mỗi truy vấn: session rẻ, đơn luồng và TUYỆT ĐỐI không được chia sẻ. Thứ
                // đắt là connection pool, mà pool nằm trong IDriver singleton.
                await using var session = _driver.AsyncSession(builder => builder
                    .WithDatabase(_config.Database)
                    .WithDefaultAccessMode(AccessMode.Read));

                // ExecuteReadAsync chứ không RunAsync trần: nó tự thử lại lỗi thoáng qua và, với
                // Aura, định tuyến sang follower. Access mode Read còn nghĩa là một lỗi trong câu
                // lệnh cũng KHÔNG ghi được gì.
                var records = await session.ExecuteReadAsync(async runner =>
                {
                    var cursor = await runner.RunAsync(query, parameters);
                    return await cursor.ToListAsync();
                },
                builder => builder.WithTimeout(TimeSpan.FromMilliseconds(_config.TransactionTimeoutMs)))
                // WaitAsync chặn TOÀN BỘ lượt tra nhìn từ phía ứng dụng. Cần riêng bên cạnh timeout
                // giao dịch vì timeout kia chỉ bắt đầu đếm khi câu lệnh ĐÃ TỚI server: nó không với
                // tới DNS hỏng, bắt tay TLS treo hay pool đã cạn.
                .WaitAsync(TimeSpan.FromMilliseconds(_config.OperationTimeoutMs), cancellationToken);

                _breaker.ReportSuccess();

                return records;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Người chơi ngắt kết nối. Ném tiếp để phần còn lại của pipeline dừng theo thay vì
                // đốt thêm một lượt gọi LLM cho một câu trả lời không ai đọc.
                throw;
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref _errors);
                _breaker.ReportFailure(exception);

                return null;
            }
        }

        private GraphEdge ToEdge(IRecord record) => new(
            record[GraphCypher.Columns.Id].As<string>(),
            record[GraphCypher.Columns.Source].As<string>(),
            record[GraphCypher.Columns.Relation].As<string>(),
            record[GraphCypher.Columns.Target].As<string>(),
            record[GraphCypher.Columns.Status].As<string>(),
            record[GraphCypher.Columns.Negated].As<bool>(),
            Strings(record, GraphCypher.Columns.ChunkCodes),
            record[GraphCypher.Columns.Priority].As<int>(),
            record[GraphCypher.Columns.Reasoning].As<int>() == 0);

        /// <summary>
        /// Sắp nhãn ở đây chứ không tin thứ tự của <c>labels(e)</c>: Neo4j không cam kết thứ tự đó, và
        /// danh mục là một phần của prompt — prompt đổi thứ tự giữa hai lần chạy là phép đo mất lặp lại.
        /// </summary>
        private GraphCatalogEntry ToCatalogEntry(IRecord record) => new(
            record[GraphCypher.Columns.Name].As<string>(),
            Strings(record, GraphCypher.Columns.Aliases),
            Strings(record, GraphCypher.Columns.Labels)
                .Where(label => !_hiddenLabels.Contains(label))
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToList(),
            record[GraphCypher.Columns.IsSelf].As<bool>());

        private static List<string> Strings(IRecord record, string column) =>
            record[column] is null
                ? new List<string>()
                : record[column].As<List<object>>().Select(value => value?.ToString() ?? string.Empty).ToList();
    }
}
