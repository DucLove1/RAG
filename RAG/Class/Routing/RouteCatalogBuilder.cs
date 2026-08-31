using RAG.Interface;
using System.Security.Cryptography;
using System.Text;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Nạp vector cho toàn bộ route, ưu tiên lấy từ cache, rồi dựng bảng route trong
    /// <see cref="RouteCatalog"/>. Được gọi bởi <see cref="SemanticRouterWarmupService"/>.
    /// </summary>
    public sealed class RouteCatalogBuilder : IRouterWarmup
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IRouteVectorCache _vectorCache;
        private readonly IRouteUtteranceStore _utteranceStore;
        private readonly RouteCatalog _catalog;
        private readonly ILogger<RouteCatalogBuilder> _logger;

        public RouteCatalogBuilder(IEmbeddingProvider embeddingProvider,
                                   IRouteVectorCache vectorCache,
                                   IRouteUtteranceStore utteranceStore,
                                   RouteCatalog catalog,
                                   ILogger<RouteCatalogBuilder> logger)
        {
            _embeddingProvider = embeddingProvider;
            _vectorCache = vectorCache;
            _utteranceStore = utteranceStore;
            _catalog = catalog;
            _logger = logger;
        }

        /// <summary>Trả về <c>true</c> khi có ít nhất một route dùng được.</summary>
        public async Task<bool> TryBuildAsync(CancellationToken cancellationToken = default)
        {
            var utterances = _catalog.CollectConfiguredUtterances();

            if (utterances.Count == 0)
            {
                _logger.LogWarning("Không có câu mẫu nào để nạp, node định tuyến sẽ không hoạt động.");
                return false;
            }

            var dims = _embeddingProvider.Dimensions;
            var fingerprint = ComputeFingerprint(dims);
            var cached = await _vectorCache.TryLoadAsync(fingerprint, cancellationToken);

            var (vectors, embeddedCount) = await EnsureVectorsAsync(utterances, cached, fingerprint, dims, cancellationToken);

            _catalog.SetConfigVectors(vectors);
            _catalog.SetAddedUtterances(await _utteranceStore.LoadAsync(cancellationToken));

            var built = _catalog.Rebuild(dims);

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
                built, _catalog.Routes!.Sum(route => route.Vectors.Count), _catalog.AddedUtterances.Count, source);

            return true;
        }

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
                    if (cached.TryGetValue(utterance, out var vector) && RouteCatalog.IsUsable(vector, dims))
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

                if (!RouteCatalog.IsUsable(vector, dims))
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
    }
}
