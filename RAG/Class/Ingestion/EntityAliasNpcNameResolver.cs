using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Extension;
using RAG.Interface;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Nối vai trò trong corpus với tên riêng qua <c>bi_danh</c> của <c>entities.json</c>.
    /// <para>
    /// Đọc đúng file mà bộ nạp đồ thị đọc, nên đổi tên một NPC vẫn chỉ là sửa một dòng ở đó — không
    /// đụng corpus, không đụng code, và Qdrant lẫn Neo4j không thể lệch tên nhau.
    /// </para>
    /// <para>
    /// Thiếu file thì lùi về giữ nguyên tên corpus kèm một cảnh báo, KHÔNG ném: lớp này nằm trên
    /// đường nạp corpus, và một máy không dùng đồ thị không nên mất luôn khả năng nạp. Ngược lại, một
    /// bí danh trỏ về HAI thực thể thì ném ngay — chọn bừa một bên là trao tri thức của NPC này cho
    /// NPC kia.
    /// </para>
    /// </summary>
    public sealed class EntityAliasNpcNameResolver : INpcNameResolver
    {
        private readonly Dictionary<string, string> _byAlias;

        public EntityAliasNpcNameResolver(IOptions<GraphConfig> options,
                                          ILogger<EntityAliasNpcNameResolver> logger)
        {
            var path = Path.Combine(ContentFilePath.Resolve(options.Value.DataPath), GraphDataFiles.Entities);

            if (!File.Exists(path))
            {
                logger.LogWarning(MissingMessage, path);
                _byAlias = new Dictionary<string, string>(StringComparer.Ordinal);
                return;
            }

            var entities = JsonSerializer.Deserialize<List<EntityDocument>>(File.ReadAllText(path))
                           ?? new List<EntityDocument>();

            _byAlias = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var entity in entities)
            {
                foreach (var alias in entity.BiDanh ?? Enumerable.Empty<string>())
                {
                    if (_byAlias.TryGetValue(alias, out var existing) && existing != entity.Ten)
                        throw new InvalidOperationException(string.Format(AmbiguousMessage, alias, existing, entity.Ten));

                    _byAlias[alias] = entity.Ten;
                }
            }
        }

        public string Resolve(string corpusName) =>
            _byAlias.TryGetValue(corpusName, out var name) ? name : corpusName;

        private const string MissingMessage =
            "Không thấy {Path}; tên NPC sẽ giữ nguyên như trong corpus và KHÔNG khớp với tên riêng trong đồ thị.";

        private const string AmbiguousMessage =
            "Bí danh '{0}' trỏ về hai thực thể '{1}' và '{2}' trong entities.json. Mỗi bí danh chỉ được " +
            "thuộc về một thực thể, nếu không phân quyền NPC không xác định được.";

        private sealed record EntityDocument(
            [property: JsonPropertyName("ten")] string Ten,
            [property: JsonPropertyName("bi_danh")] List<string>? BiDanh);
    }
}
