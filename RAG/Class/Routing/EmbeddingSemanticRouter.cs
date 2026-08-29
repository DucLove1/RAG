using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;
using System.Security.Cryptography;
using System.Text;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Định tuyến bằng cosine similarity trên các vector câu mẫu nạp sẵn trong RAM.
    /// <para>
    /// Vector được nạp bởi <see cref="SemanticRouterWarmupService"/> chạy nền, nên khởi động ứng dụng
    /// không phụ thuộc vào nhà cung cấp embedding. Trước khi nạp xong — và trong mọi trường hợp lỗi —
    /// router trả <c>null</c>, tức là fail-open về đường RAG, giống cách LlmQueryNormalizer
    /// fail-open về câu hỏi gốc.
    /// </para>
    /// </summary>
    public sealed class EmbeddingSemanticRouter : ISemanticRouter
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IRouteVectorCache _vectorCache;
        private readonly IRouteUtteranceStore _utteranceStore;
        private readonly SemanticRouterConfig _config;
        private readonly PromptConfig _promptConfig;
        private readonly ILogger<EmbeddingSemanticRouter> _logger;

        /// <summary>Nối tiếp các lần thêm câu mẫu, để hai request đồng thời không ghi đè lên nhau.</summary>
        private readonly SemaphoreSlim _updateLock = new(1, 1);

        /// <summary>
        /// Bất biến sau khi gán, và chỉ được gán bằng một phép gán tham chiếu duy nhất.
        /// Nhờ đó singleton này an toàn với truy cập đồng thời mà không cần khóa khi ĐỌC.
        /// <c>null</c> nghĩa là chưa nạp xong — mọi request đi đường RAG.
        /// </summary>
        private volatile IReadOnlyList<RouteVectors>? _routes;

        /// <summary>Vector của các câu mẫu khai báo trong cấu hình; giữ lại để dựng lại route khi có câu mới.</summary>
        private volatile IReadOnlyDictionary<string, float[]>? _configVectors;

        /// <summary>Câu mẫu thêm lúc chạy; giữ lại để ghi đè toàn bộ file khi có thay đổi.</summary>
        private volatile IReadOnlyList<StoredUtterance> _addedUtterances = Array.Empty<StoredUtterance>();

        public EmbeddingSemanticRouter(
            IEmbeddingProvider embeddingProvider,
            IRouteVectorCache vectorCache,
            IRouteUtteranceStore utteranceStore,
            IOptions<SemanticRouterConfig> options,
            IOptions<PromptConfig> promptOptions,
            ILogger<EmbeddingSemanticRouter> logger)
        {
            _embeddingProvider = embeddingProvider;
            _vectorCache = vectorCache;
            _utteranceStore = utteranceStore;
            _config = options.Value;
            _promptConfig = promptOptions.Value;
            _logger = logger;
        }

        public RouteMatch? Route(string question, float[] questionEmbedding)
        {
            var routes = _routes;

            if (routes is null || routes.Count == 0)
                return null;

            if (!IsRoutable(question, questionEmbedding))
                return null;

            var best = FindBest(routes, questionEmbedding);

            if (best is null)
            {
                _logger.LogDebug("Không route nào vượt ngưỡng cho câu \"{Question}\", dùng đường RAG.", question);
                return null;
            }

            _logger.LogDebug("Định tuyến khớp route {Route} (score={Score:F3}), bỏ qua truy hồi.",
                best.Route.Name, best.Score);

            return new RouteMatch(best.Route.Name, best.Score,
                best.Route.SystemPromptTemplate, best.Route.UserPromptTemplate);
        }

        public IReadOnlyList<RouteScore> Explain(string question, float[] questionEmbedding)
        {
            var routes = _routes;

            if (routes is null || routes.Count == 0)
                return Array.Empty<RouteScore>();

            var routable = IsRoutable(question, questionEmbedding);

            return routes
                .Select(route =>
                {
                    var score = ScoreRoute(route, questionEmbedding);
                    return new RouteScore(route.Name, score, route.Threshold,
                        Matched: routable && score >= route.Threshold);
                })
                .OrderByDescending(score => score.Score)
                .ToList();
        }

        /// <summary>
        /// Thêm câu mẫu vào một route đang chạy: nhúng các câu dạng text, nhận thẳng các vector đã có,
        /// lưu xuống kho rồi dựng lại bảng route.
        /// <para>
        /// Dựng lại theo kiểu copy-on-write — tạo danh sách mới rồi gán một lần — nên đường đọc vẫn
        /// không cần khóa và request đang chạy không bao giờ thấy trạng thái nửa vời.
        /// </para>
        /// </summary>
        public async Task<RouteUpdateResult> AddUtterancesAsync(string routeName,
                                                                IReadOnlyList<string> utterances,
                                                                IReadOnlyList<float[]> vectors,
                                                                CancellationToken cancellationToken = default)
        {
            var configured = _config.Routes.FirstOrDefault(route =>
                string.Equals(route.Name, routeName, StringComparison.OrdinalIgnoreCase));

            if (configured is null)
            {
                var known = string.Join(", ", _config.Routes.Select(route => route.Name));
                return new RouteUpdateResult(false, $"Không có route tên \"{routeName}\". Các route hiện có: {known}.",
                    0, 0, 0, false);
            }

            if (_routes is null)
                return new RouteUpdateResult(false, "Router chưa nạp xong vector, thử lại sau.", 0, 0, 0, false);

            var dims = await _embeddingProvider.GetDimsAsync();

            await _updateLock.WaitAsync(cancellationToken);
            try
            {
                var existing = CollectUtterances()
                    .Concat(_addedUtterances.Select(entry => entry.Text))
                    .ToHashSet(StringComparer.Ordinal);

                var fresh = new List<StoredUtterance>();
                var skipped = 0;

                // Vector nạp thẳng: chỉ kiểm tra tính hợp lệ, không tốn lượt gọi API.
                for (var i = 0; i < vectors.Count; i++)
                {
                    var label = $"[vector:{configured.Name}:{_addedUtterances.Count + fresh.Count}]";

                    if (!IsUsable(vectors[i], dims))
                    {
                        _logger.LogWarning("Vector thứ {Index} không hợp lệ (cần {Dims} chiều, khác 0), bỏ qua.", i, dims);
                        skipped++;
                        continue;
                    }

                    fresh.Add(new StoredUtterance(configured.Name, label, vectors[i]));
                }

                // Câu dạng text: bỏ trùng TRƯỚC khi gọi API để không tốn lượt vô ích.
                var toEmbed = utterances
                    .Select(utterance => utterance.Trim())
                    .Where(utterance => !string.IsNullOrWhiteSpace(utterance))
                    .Distinct(StringComparer.Ordinal)
                    .Where(utterance => !existing.Contains(utterance))
                    .ToList();

                skipped += utterances.Count(utterance => !string.IsNullOrWhiteSpace(utterance)) - toEmbed.Count;

                if (toEmbed.Count > 0)
                {
                    var embeddings = await _embeddingProvider.GetEmbeddingsBatchAsync(toEmbed, cancellationToken);

                    for (var i = 0; i < toEmbed.Count && i < embeddings.Count; i++)
                    {
                        if (!IsUsable(embeddings[i], dims))
                        {
                            _logger.LogWarning("Câu mẫu \"{Utterance}\" nhận vector không hợp lệ, bỏ qua.", toEmbed[i]);
                            skipped++;
                            continue;
                        }

                        fresh.Add(new StoredUtterance(configured.Name, toEmbed[i], embeddings[i]));
                    }
                }

                if (fresh.Count == 0)
                {
                    var total = CountUtterances(configured.Name);
                    return new RouteUpdateResult(false, "Không có câu mẫu nào được thêm (trùng lặp hoặc vector không hợp lệ).",
                        0, skipped, total, false);
                }

                var merged = _addedUtterances.Concat(fresh).ToList();
                var persisted = await _utteranceStore.SaveAsync(merged, cancellationToken);

                _addedUtterances = merged;
                RebuildRoutes(dims);

                var totalInRoute = CountUtterances(configured.Name);

                var message = persisted
                    ? $"Đã thêm {fresh.Count} câu mẫu vào route \"{configured.Name}\"."
                    : $"Đã thêm {fresh.Count} câu mẫu vào route \"{configured.Name}\", nhưng KHÔNG lưu được xuống đĩa nên sẽ mất khi khởi động lại.";

                _logger.LogInformation("{Message} Tổng câu mẫu của route: {Total}.", message, totalInRoute);

                return new RouteUpdateResult(true, message, fresh.Count, skipped, totalInRoute, persisted);
            }
            finally
            {
                _updateLock.Release();
            }
        }

        /// <summary>
        /// Nạp vector cho toàn bộ route, ưu tiên lấy từ cache. Gọi bởi warm-up service.
        /// Trả về <c>true</c> khi có ít nhất một route dùng được.
        /// </summary>
        internal async Task<bool> TryBuildAsync(CancellationToken cancellationToken)
        {
            var utterances = CollectUtterances();

            if (utterances.Count == 0)
            {
                _logger.LogWarning("Không có câu mẫu nào để nạp, node định tuyến sẽ không hoạt động.");
                return false;
            }

            var dims = await _embeddingProvider.GetDimsAsync();
            var fingerprint = ComputeFingerprint(dims);
            var cached = await _vectorCache.TryLoadAsync(fingerprint, cancellationToken);

            var (vectors, embeddedCount) = await EnsureVectorsAsync(utterances, cached, fingerprint, dims, cancellationToken);

            _configVectors = vectors;
            _addedUtterances = await _utteranceStore.LoadAsync(cancellationToken);

            var built = RebuildRoutes(dims);

            if (built == 0)
            {
                _logger.LogWarning("Không dựng được route nào từ {Count} câu mẫu.", utterances.Count);
                return false;
            }

            var source = embeddedCount == 0
                ? "cache"
                : embeddedCount == utterances.Count ? "API" : $"cache + {embeddedCount} câu mới qua API";

            _logger.LogInformation("Semantic router đã nạp {RouteCount} route / {VectorCount} câu mẫu " +
                                   "({Added} câu thêm lúc chạy, nguồn: {Source}).",
                built, _routes!.Sum(route => route.Vectors.Count), _addedUtterances.Count, source);

            return true;
        }

        private bool IsRoutable(string question, float[] questionEmbedding)
        {
            if (questionEmbedding.Length == 0)
                return false;

            // Câu dài gần như luôn là câu hỏi tri thức, kể cả khi mở đầu bằng một lời chào.
            if (question.Length > _config.MaxRoutableLength)
                return false;

            return true;
        }

        private static BestMatch? FindBest(IReadOnlyList<RouteVectors> routes, float[] questionEmbedding)
        {
            BestMatch? best = null;

            foreach (var route in routes)
            {
                var score = ScoreRoute(route, questionEmbedding);

                if (score < route.Threshold)
                    continue;

                if (best is null || score > best.Score)
                    best = new BestMatch(route, score);
            }

            return best;
        }

        /// <summary>
        /// Điểm của route = điểm CAO NHẤT trong các câu mẫu của nó, không phải trung bình.
        /// Liệt kê nhiều câu mẫu là để phủ nhiều cách nói; lấy trung bình sẽ trừng phạt đúng những
        /// route được viết kỹ và làm các route có số câu mẫu khác nhau không còn so sánh được với
        /// cùng một ngưỡng.
        /// </summary>
        private static double ScoreRoute(RouteVectors route, float[] questionEmbedding)
        {
            var max = 0d;

            foreach (var vector in route.Vectors)
            {
                var score = VectorMath.CosineSimilarity(questionEmbedding, vector);
                if (score > max) max = score;
            }

            return max;
        }

        private List<string> CollectUtterances() =>
            _config.Routes
                .SelectMany(route => route.Utterances)
                .Where(utterance => !string.IsNullOrWhiteSpace(utterance))
                .Distinct(StringComparer.Ordinal)
                .ToList();

        private int CountUtterances(string routeName) =>
            _routes?.FirstOrDefault(route => string.Equals(route.Name, routeName, StringComparison.OrdinalIgnoreCase))
                ?.Vectors.Count ?? 0;

        /// <summary>
        /// Ghép vector từ cache với vector nhúng mới, và CHỈ nhúng những câu mẫu chưa có trong cache.
        /// <para>
        /// Nhờ vậy việc thêm một route hay sửa vài câu mẫu chỉ tốn đúng số câu mới, thay vì nhúng lại
        /// toàn bộ — mà sửa câu mẫu chính là việc sẽ làm nhiều nhất khi tinh chỉnh.
        /// </para>
        /// </summary>
        /// <returns>Bộ vector đầy đủ, kèm số câu đã phải gọi API để nhúng.</returns>
        private async Task<(IReadOnlyDictionary<string, float[]> Vectors, int EmbeddedCount)> EnsureVectorsAsync(
            IReadOnlyList<string> utterances,
            IReadOnlyDictionary<string, float[]>? cached,
            string fingerprint,
            int dims,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, float[]>(utterances.Count, StringComparer.Ordinal);

            // Chỉ giữ lại câu mẫu còn trong cấu hình: cache không phình vô hạn theo các lần chỉnh sửa.
            // Vector lấy từ cache vẫn phải qua đúng bộ kiểm tra như vector mới, phòng file bị hỏng.
            if (cached is not null)
            {
                foreach (var utterance in utterances)
                {
                    if (cached.TryGetValue(utterance, out var vector) && IsUsable(vector, dims))
                        result[utterance] = vector;
                }
            }

            var missing = utterances.Where(utterance => !result.ContainsKey(utterance)).ToList();

            if (missing.Count == 0)
                return (result, 0);

            _logger.LogInformation("Cần nhúng {Missing}/{Total} câu mẫu (phần còn lại lấy từ cache).",
                missing.Count, utterances.Count);

            var embeddings = await _embeddingProvider.GetEmbeddingsBatchAsync(missing, cancellationToken);
            var allValid = embeddings.Count == missing.Count;

            if (!allValid)
            {
                _logger.LogWarning("Nhận {Actual} vector cho {Expected} câu mẫu, sẽ không ghi cache.",
                    embeddings.Count, missing.Count);
            }

            for (var i = 0; i < missing.Count && i < embeddings.Count; i++)
            {
                var vector = embeddings[i];

                if (!IsUsable(vector, dims))
                {
                    _logger.LogWarning("Câu mẫu \"{Utterance}\" nhận vector không hợp lệ, bỏ qua.", missing[i]);
                    allValid = false;
                    continue;
                }

                result[missing[i]] = vector;
            }

            // Chỉ ghi cache khi TOÀN BỘ câu còn thiếu đều nhúng thành công: ghi cache thiếu sẽ khiến
            // lần khởi động sau tưởng đã đủ và không bao giờ nhúng lại phần hỏng.
            if (allValid)
                await _vectorCache.SaveAsync(fingerprint, result, cancellationToken);

            return (result, missing.Count);
        }

        /// <summary>
        /// Dựng lại bảng route từ vector cấu hình cộng câu mẫu thêm lúc chạy, rồi gán một lần
        /// vào <see cref="_routes"/>. Trả về số route dựng được.
        /// </summary>
        private int RebuildRoutes(int dims)
        {
            var configVectors = _configVectors ?? new Dictionary<string, float[]>(StringComparer.Ordinal);

            var addedByRoute = _addedUtterances
                .GroupBy(entry => entry.Route, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            var built = new List<RouteVectors>(_config.Routes.Count);

            foreach (var route in _config.Routes)
            {
                if (string.IsNullOrWhiteSpace(route.Name) || string.IsNullOrWhiteSpace(route.UserPromptTemplate))
                {
                    _logger.LogWarning("Bỏ qua route thiếu Name hoặc UserPromptTemplate.");
                    continue;
                }

                if (!route.UserPromptTemplate.Contains("{0}", StringComparison.Ordinal))
                {
                    _logger.LogWarning("UserPromptTemplate của route {Route} không chứa {{0}}, câu hỏi sẽ bị bỏ khỏi prompt.",
                        route.Name);
                }

                var routeVectors = route.Utterances
                    .Where(utterance => !string.IsNullOrWhiteSpace(utterance))
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

                var systemTemplate = string.IsNullOrWhiteSpace(route.SystemPromptTemplate)
                    ? _promptConfig.AnswerSystemTemplate
                    : route.SystemPromptTemplate;

                built.Add(new RouteVectors(
                    route.Name,
                    route.SimilarityThreshold ?? _config.SimilarityThreshold,
                    systemTemplate,
                    route.UserPromptTemplate,
                    routeVectors));
            }

            _routes = built;
            return built.Count;
        }

        /// <summary>
        /// GeminiEmbeddingProvider trả mảng rỗng khi API lỗi thay vì ném exception, nên vector rác
        /// phải bị chặn ở đây — nếu không nó sẽ được ghi vào cache và tồn tại qua mọi lần khởi động.
        /// </summary>
        private static bool IsUsable(float[]? vector, int dims) =>
            vector is not null && vector.Length == dims && VectorMath.HasMagnitude(vector);

        /// <summary>
        /// Vân tay chỉ gồm model và số chiều — những thứ làm cho TOÀN BỘ vector cũ trở nên vô giá trị.
        /// <para>
        /// Cố tình KHÔNG băm tập câu mẫu: từng câu mẫu đã là khóa riêng trong cache, nên thêm hay bớt
        /// câu chỉ cần nhúng phần chênh lệch. Prompt template và ngưỡng cũng không nằm trong vân tay
        /// vì chúng không ảnh hưởng tới giá trị vector.
        /// </para>
        /// </summary>
        private string ComputeFingerprint(int dims)
        {
            var payload = $"{_embeddingProvider.ModelId}\n{dims}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash);
        }

        private sealed record RouteVectors(
            string Name,
            double Threshold,
            string SystemPromptTemplate,
            string UserPromptTemplate,
            IReadOnlyList<float[]> Vectors);

        private sealed record BestMatch(RouteVectors Route, double Score);
    }
}
