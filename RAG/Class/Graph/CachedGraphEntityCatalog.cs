using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Danh mục thực thể theo NPC, đọc từ Neo4j và giữ trong RAM.
    /// <para>
    /// Lấy từ Neo4j chứ không đọc <c>entities.json</c>: <c>duoc_biet</c> chỉ sống trong đồ thị, và file
    /// dữ liệu chỉ được nạp ở nhánh quản trị. Danh mục chỉ đổi khi nạp lại đồ thị, nhưng được đọc ở
    /// mọi câu hỏi trượt cache câu trả lời — không cache là thêm một round-trip Neo4j cho mỗi câu.
    /// </para>
    /// <para>
    /// Cache là một từ điển nhỏ chứ không phải <c>MemoryCache</c>: chỉ có cỡ chục khóa. Ba luật:
    /// </para>
    /// <para>
    /// 1. KHÓA PHÂN BIỆT HOA THƯỜNG. Cypher so <c>npc.ten = $npc</c> có phân biệt, nên "edward" ra
    /// danh mục rỗng còn "Edward" ra đủ; gộp hai khóa là để một request gõ sai làm hỏng request đúng.
    /// </para>
    /// <para>
    /// 2. KHÔNG CACHE LỖI. Neo4j chập chờn một lần mà bị đóng băng lại thì hết thời hạn cache vẫn trả
    /// lỗi dù Neo4j đã sống lại.
    /// </para>
    /// <para>
    /// 3. KHÔNG CACHE DANH MỤC RỖNG. Tên NPC do client gửi lên; cache cả tên lạ thì từ điển phình không
    /// giới hạn. Chỉ cache danh mục có nội dung thì số khóa bị chặn bởi số NPC thật và bí danh của họ.
    /// </para>
    /// </summary>
    public sealed class CachedGraphEntityCatalog : IGraphEntityCatalog
    {
        private readonly IGraphStore _store;
        private readonly ILogger<CachedGraphEntityCatalog> _logger;
        private readonly long _ttlMilliseconds;
        private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

        public CachedGraphEntityCatalog(IGraphStore store,
                                        IOptions<GraphExtractionConfig> options,
                                        ILogger<CachedGraphEntityCatalog> logger)
        {
            _store = store;
            _logger = logger;
            _ttlMilliseconds = (long)TimeSpan.FromMinutes(options.Value.CatalogCacheMinutes).TotalMilliseconds;
        }

        public async Task<NpcEntityCatalog> GetAsync(string npcName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return NpcEntityCatalog.Empty;

            // TickCount64 chứ không phải giờ hệ thống: đơn điệu, nên chỉnh đồng hồ máy không làm cả
            // loạt entry hết hạn cùng lúc hay sống mãi.
            var now = Environment.TickCount64;

            if (_entries.TryGetValue(npcName, out var cached) && cached.ExpiresAt > now)
                return cached.Catalog;

            var result = await _store.ListKnownEntitiesAsync(npcName, cancellationToken);

            if (result.Failed)
                return NpcEntityCatalog.Unavailable;

            var catalog = NpcEntityCatalog.From(result.Items);

            if (catalog.IsEmpty)
            {
                _logger.LogDebug("Không có danh mục thực thể nào cho NPC {Npc}: tên không khớp node NPC nào.", npcName);
                return catalog;
            }

            if (catalog.AmbiguousKeys.Count > 0)
            {
                _logger.LogWarning(
                    "Danh mục của {Npc} có {Count} cách gọi trỏ tới hơn một thực thể nên đã bị bỏ: {Keys}.",
                    npcName, catalog.AmbiguousKeys.Count, string.Join(", ", catalog.AmbiguousKeys));
            }

            _entries[npcName] = new CacheEntry(catalog, now + _ttlMilliseconds);

            return catalog;
        }

        private sealed record CacheEntry(NpcEntityCatalog Catalog, long ExpiresAt);
    }
}
