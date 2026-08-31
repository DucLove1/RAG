using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Thêm câu mẫu vào một route đang chạy: nhúng các câu dạng text, nhận thẳng các vector đã có,
    /// lưu xuống kho rồi dựng lại bảng route.
    /// <para>
    /// Dựng lại theo kiểu copy-on-write — tạo danh sách mới rồi gán một lần — nên đường đọc của
    /// router vẫn không cần khóa và request đang chạy không bao giờ thấy trạng thái nửa vời.
    /// </para>
    /// </summary>
    public sealed class RouteUtteranceAdmin
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IRouteUtteranceStore _utteranceStore;
        private readonly RouteCatalog _catalog;
        private readonly SemanticRouterConfig _config;
        private readonly ILogger<RouteUtteranceAdmin> _logger;

        /// <summary>Nối tiếp các lần thêm câu mẫu, để hai request đồng thời không ghi đè lên nhau.</summary>
        private readonly SemaphoreSlim _updateLock = new(1, 1);

        public RouteUtteranceAdmin(IEmbeddingProvider embeddingProvider,
                                   IRouteUtteranceStore utteranceStore,
                                   RouteCatalog catalog,
                                   IOptions<SemanticRouterConfig> options,
                                   ILogger<RouteUtteranceAdmin> logger)
        {
            _embeddingProvider = embeddingProvider;
            _utteranceStore = utteranceStore;
            _catalog = catalog;
            _config = options.Value;
            _logger = logger;
        }

        public async Task<RouteUpdateResult> AddUtterancesAsync(string routeName,
                                                                IReadOnlyList<string> utterances,
                                                                IReadOnlyList<float[]> vectors,
                                                                CancellationToken cancellationToken = default)
        {
            var configured = _config.Routes.FirstOrDefault(route =>
                string.Equals(route.Name, routeName, StringComparison.OrdinalIgnoreCase));

            if (configured is null)
            {
                var known = _config.Routes.Select(route => route.Name).ToList();
                return RouteUpdateResult.UnknownRoute(routeName, known);
            }

            if (_catalog.Routes is null)
                return RouteUpdateResult.NotReady();

            var dims = _embeddingProvider.Dimensions;

            await _updateLock.WaitAsync(cancellationToken);
            try
            {
                var existing = _catalog.CollectConfiguredUtterances()
                    .Concat(_catalog.AddedUtterances.Select(entry => entry.Text))
                    .ToHashSet(StringComparer.Ordinal);

                var fresh = new List<StoredUtterance>();
                var skipped = 0;

                // Vector nạp thẳng: chỉ kiểm tra tính hợp lệ, không tốn lượt gọi API.
                for (var i = 0; i < vectors.Count; i++)
                {
                    var label = $"[vector:{configured.Name}:{_catalog.AddedUtterances.Count + fresh.Count}]";

                    if (!RouteCatalog.IsUsable(vectors[i], dims))
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
                        if (!RouteCatalog.IsUsable(embeddings[i], dims))
                        {
                            _logger.LogWarning("Câu mẫu \"{Utterance}\" nhận vector không hợp lệ, bỏ qua.", toEmbed[i]);
                            skipped++;
                            continue;
                        }

                        fresh.Add(new StoredUtterance(configured.Name, toEmbed[i], embeddings[i]));
                    }
                }

                if (fresh.Count == 0)
                    return RouteUpdateResult.NothingAdded(skipped, _catalog.CountUtterances(configured.Name));

                var merged = _catalog.AddedUtterances.Concat(fresh).ToList();
                var persisted = await _utteranceStore.SaveAsync(merged, cancellationToken);

                _catalog.SetAddedUtterances(merged);
                _catalog.Rebuild(dims);

                var totalInRoute = _catalog.CountUtterances(configured.Name);

                _logger.LogInformation("Đã thêm {Added} câu mẫu vào route {Route} (lưu xuống đĩa: {Persisted}). " +
                                       "Tổng câu mẫu của route: {Total}.",
                    fresh.Count, configured.Name, persisted, totalInRoute);

                return RouteUpdateResult.Succeeded(configured.Name, fresh.Count, skipped, totalInRoute, persisted);
            }
            finally
            {
                _updateLock.Release();
            }
        }
    }
}
