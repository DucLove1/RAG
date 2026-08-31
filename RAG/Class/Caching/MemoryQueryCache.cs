using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Cache trong RAM, có chặn trần số entry và tự đẩy entry nguội ra, kèm khả năng xuất/nạp
    /// snapshot để <see cref="QueryCachePersistenceService"/> lưu xuống đĩa.
    /// </summary>
    public sealed class MemoryQueryCache
        : INormalizationCache, IEmbeddingCache, IRouteDecisionCache, IQueryCacheStatistics, IPersistableQueryCache, IDisposable
    {
        private const string NormalizationPrefix = "norm:";
        private const string EmbeddingPrefix = "emb:";
        private const string RoutePrefix = "route:";

        private readonly MemoryCache _cache;
        private readonly QueryCacheConfig _config;
        private readonly int _dims;
        private readonly string _embeddingKeyPrefix;

        /// <summary>
        /// Chỉ mục song song, chỉ phục vụ việc xuất snapshot.
        /// <para>
        /// KHÔNG dùng <c>MemoryCache.Keys</c> (có sẵn từ .NET 9) cho việc này: muốn lấy giá trị vẫn
        /// phải gọi <c>TryGetValue</c>, mà việc đó LÀM MỚI sliding expiration của mọi entry được
        /// duyệt qua. Flush định kỳ vài phút một lần sẽ khiến không entry nào hết hạn nữa — phá đúng
        /// cơ chế thời hạn ngắn đang bảo vệ kết quả chuẩn hóa fail-open.
        /// </para>
        /// Đọc từ chỉ mục này thì không đụng gì tới thời hạn của cache.
        /// </summary>
        private readonly ConcurrentDictionary<string, NormalizationEntry> _normalizationIndex = new(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, EmbeddingEntry> _embeddingIndex = new(StringComparer.Ordinal);

        private long _normalizationHits;
        private long _normalizationMisses;
        private long _embeddingHits;
        private long _embeddingMisses;
        private long _routeHits;
        private long _routeMisses;

        /// <summary>Đếm số lần ghi, để service flush biết có gì mới đáng lưu hay không.</summary>
        private long _writeCount;

        /// <param name="embeddingModelId">
        /// Truyền thẳng từ composition root chứ KHÔNG lấy qua <c>IEmbeddingProvider</c>: provider đã
        /// bị bọc bởi decorator cache, nên phụ thuộc ngược lại sẽ tạo vòng lặp trong container.
        /// </param>
        public MemoryQueryCache(string embeddingModelId,
                                int embeddingDimensions,
                                IOptions<QueryCacheConfig> options)
        {
            _config = options.Value;
            _dims = embeddingDimensions;

            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = Math.Max(1, _config.MaxEntries)
            });

            // Model nằm trong khóa để vector của model cũ không bao giờ bị dùng nhầm cho model mới.
            _embeddingKeyPrefix = EmbeddingPrefix + embeddingModelId + ":";

            Fingerprint = ComputeFingerprint(embeddingModelId, embeddingDimensions);
        }

        /// <summary>
        /// Vân tay của dữ liệu cache: model và số chiều — hai thứ làm cho toàn bộ vector cũ trở nên
        /// vô giá trị. Dùng để bỏ file cache trên đĩa khi cấu hình embedding thay đổi.
        /// </summary>
        public string Fingerprint { get; }

        /// <summary>Service flush so sánh giá trị này với lần trước để biết có cần ghi đĩa không.</summary>
        public long WriteCount => Interlocked.Read(ref _writeCount);

        public void Dispose() => _cache.Dispose();

        public bool TryGetNormalizedQuestion(string question, out string normalized)
        {
            if (_cache.TryGetValue(NormalizationPrefix + question, out string? cached) && cached is not null)
            {
                Interlocked.Increment(ref _normalizationHits);
                Touch(_normalizationIndex, question);
                normalized = cached;
                return true;
            }

            Interlocked.Increment(ref _normalizationMisses);
            normalized = string.Empty;
            return false;
        }

        public void SetNormalizedQuestion(string question, string normalized, bool unchanged)
        {
            var minutes = unchanged
                ? _config.UnchangedNormalizationExpirationMinutes
                : _config.SlidingExpirationMinutes;

            _cache.Set(NormalizationPrefix + question, normalized, BuildOptions(minutes, (_, _, reason, _) =>
            {
                // Reason Replaced nghĩa là entry mới vừa ghi đè lên entry cũ. Callback chạy trên
                // thread pool nên có thể tới SAU khi chỉ mục đã cập nhật; xoá lúc đó là mất entry mới.
                if (reason != EvictionReason.Replaced)
                    _normalizationIndex.TryRemove(question, out _);
            }));

            _normalizationIndex[question] = new NormalizationEntry(normalized, unchanged)
            {
                LastAccessTicks = Environment.TickCount64
            };

            Interlocked.Increment(ref _writeCount);
        }

        public bool TryGetEmbedding(string text, out float[] vector)
        {
            if (_cache.TryGetValue(_embeddingKeyPrefix + text, out float[]? cached) && cached is not null)
            {
                Interlocked.Increment(ref _embeddingHits);
                Touch(_embeddingIndex, text);
                vector = cached;
                return true;
            }

            Interlocked.Increment(ref _embeddingMisses);
            vector = Array.Empty<float>();
            return false;
        }

        public void SetEmbedding(string text, float[] vector)
        {
            _cache.Set(_embeddingKeyPrefix + text, vector, BuildOptions(_config.SlidingExpirationMinutes, (_, _, reason, _) =>
            {
                if (reason != EvictionReason.Replaced)
                    _embeddingIndex.TryRemove(text, out _);
            }));

            _embeddingIndex[text] = new EmbeddingEntry(vector)
            {
                LastAccessTicks = Environment.TickCount64
            };

            Interlocked.Increment(ref _writeCount);
        }

        public bool TryGetRoute(string question, out RouteMatch? route)
        {
            if (_cache.TryGetValue(RoutePrefix + question, out RouteDecision? cached) && cached is not null)
            {
                Interlocked.Increment(ref _routeHits);
                route = cached.Match;
                return true;
            }

            Interlocked.Increment(ref _routeMisses);
            route = null;
            return false;
        }

        /// <summary>
        /// Lưu quyết định định tuyến. Bọc trong <see cref="RouteDecision"/> để một quyết định
        /// "không route nào khớp" vẫn là một giá trị có mặt trong cache — <c>MemoryCache</c> không
        /// phân biệt được <c>null</c> đã lưu với entry chưa từng tồn tại.
        /// <para>
        /// KHÔNG tăng <c>_writeCount</c>, và cũng KHÔNG có mặt trong <see cref="ExportSnapshot"/>:
        /// quyết định định tuyến chỉ sống trong RAM. Lý do là vân tay của file cache chỉ gồm model
        /// và số chiều embedding — nó không nói gì về bảng route, nên một quyết định lưu xuống đĩa
        /// sẽ sống lại sau khi người ta sửa mô tả route hay prompt phân loại. Cho vân tay bao luôn
        /// bảng route thì mỗi lần chỉnh prompt lại vứt sạch phần vector, tức là phá đúng thứ mà
        /// file cache sinh ra để giữ. Tăng <c>_writeCount</c> còn khiến service flush ghi lại toàn
        /// bộ file cho dữ liệu vốn không nằm trong file.
        /// </para>
        /// </summary>
        public void SetRoute(string question, RouteMatch? route)
        {
            // Quyết định âm nhận thời hạn ngắn hơn: router fail-open nên "LLM quyết định đi đường
            // RAG" và "LLM vừa lỗi" không phân biệt được từ bên ngoài. Cùng cơ chế đang bảo vệ kết
            // quả chuẩn hóa giống hệt câu gốc.
            var minutes = route is null
                ? _config.NoRouteExpirationMinutes
                : _config.RouteDecisionExpirationMinutes;

            _cache.Set(RoutePrefix + question, new RouteDecision(route), BuildOptions(minutes, (_, _, _, _) => { }));
        }

        public QueryCacheStats GetStats() => new(
            Interlocked.Read(ref _normalizationHits),
            Interlocked.Read(ref _normalizationMisses),
            Interlocked.Read(ref _embeddingHits),
            Interlocked.Read(ref _embeddingMisses),
            Interlocked.Read(ref _routeHits),
            Interlocked.Read(ref _routeMisses));

        /// <summary>
        /// Chụp lại N entry được dùng gần đây nhất của mỗi loại. Trần áp riêng cho từng loại: vector
        /// tốn khoảng 3KB mỗi cái nên là phần quyết định dung lượng file, còn kết quả chuẩn hóa chỉ
        /// khoảng 100 byte nên giữ nhiều cũng không đáng kể.
        /// <para>
        /// Cố tình KHÔNG có quyết định định tuyến: lý do ghi ở <see cref="SetRoute"/>. Nhờ vậy định
        /// dạng file trên đĩa không đổi và file cache cũ vẫn đọc được.
        /// </para>
        /// </summary>
        public QueryCacheSnapshot ExportSnapshot(int maxEntries)
        {
            var limit = Math.Max(0, maxEntries);

            var normalizations = _normalizationIndex
                .OrderByDescending(pair => pair.Value.LastAccessTicks)
                .Take(limit)
                .Select(pair => new StoredNormalization(pair.Key, pair.Value.Normalized, pair.Value.Unchanged))
                .ToList();

            var embeddings = _embeddingIndex
                .OrderByDescending(pair => pair.Value.LastAccessTicks)
                .Take(limit)
                .Select(pair => new StoredEmbedding(pair.Key, pair.Value.Vector))
                .ToList();

            return new QueryCacheSnapshot(normalizations, embeddings);
        }

        /// <summary>
        /// Nạp snapshot từ đĩa vào cache. Trả về số entry thực sự nạp được.
        /// <para>
        /// Vector nạp từ file phải qua đúng bộ kiểm tra như vector mới. Trước khi có lưu đĩa, một
        /// vector rác chỉ sống tới lần khởi động lại; giờ nó sẽ sống mãi nếu lọt được vào file.
        /// Phòng cả trường hợp file bị sửa tay hoặc hỏng.
        /// </para>
        /// </summary>
        public int ImportSnapshot(QueryCacheSnapshot snapshot)
        {
            var imported = 0;

            foreach (var entry in snapshot.Normalizations)
            {
                if (string.IsNullOrWhiteSpace(entry.Question) || string.IsNullOrWhiteSpace(entry.Normalized))
                    continue;

                SetNormalizedQuestion(entry.Question, entry.Normalized, entry.Unchanged);
                imported++;
            }

            foreach (var entry in snapshot.Embeddings)
            {
                if (string.IsNullOrWhiteSpace(entry.Text))
                    continue;

                if (entry.Vector.Length != _dims || !VectorMath.HasMagnitude(entry.Vector))
                    continue;

                SetEmbedding(entry.Text, entry.Vector);
                imported++;
            }

            // Nạp từ đĩa không phải là thay đổi cần ghi ngược xuống đĩa.
            Interlocked.Exchange(ref _writeCount, 0);

            return imported;
        }

        private static void Touch<TEntry>(ConcurrentDictionary<string, TEntry> index, string key)
            where TEntry : IndexEntry
        {
            if (index.TryGetValue(key, out var entry))
                entry.LastAccessTicks = Environment.TickCount64;
        }

        private static MemoryCacheEntryOptions BuildOptions(int minutes, PostEvictionDelegate onEvicted)
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(Math.Max(1, minutes))
            };

            options.RegisterPostEvictionCallback(onEvicted);

            return options;
        }

        private static string ComputeFingerprint(string modelId, int dims)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(modelId + "\n" + dims));
            return Convert.ToHexString(hash);
        }

        private abstract class IndexEntry
        {
            public long LastAccessTicks;
        }

        private sealed class NormalizationEntry : IndexEntry
        {
            public NormalizationEntry(string normalized, bool unchanged)
            {
                Normalized = normalized;
                Unchanged = unchanged;
            }

            public string Normalized { get; }
            public bool Unchanged { get; }
        }

        private sealed class EmbeddingEntry : IndexEntry
        {
            public EmbeddingEntry(float[] vector) => Vector = vector;

            public float[] Vector { get; }
        }

        /// <summary>
        /// Bọc quyết định định tuyến để phân biệt "đã quyết định là không route nào khớp" với
        /// "chưa từng gặp câu này" — <c>MemoryCache</c> trả về <c>null</c> cho cả hai.
        /// </summary>
        private sealed record RouteDecision(RouteMatch? Match);
    }
}
