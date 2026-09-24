using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Extension;
using RAG.Interface;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Đọc <c>entities.json</c> và <c>relationships.json</c> rồi kiểm tám điều trước khi cho phép
    /// ghi một dòng nào.
    /// <para>
    /// Phép kiểm mạnh nhất là số 7: MỌI <c>nguon_chunk</c> phải trỏ tới một mã chunk CÓ THẬT, đối
    /// chiếu trực tiếp với những mã mà đường nạp corpus sinh ra. Nó bắt cùng lúc hai lớp lỗi hoàn
    /// toàn khác nhau — gõ sai mã, và luật cắt dòng bị chệch — mà cả hai đều không có triệu chứng
    /// nào khác ngoài việc local search lặng lẽ trả về rỗng.
    /// </para>
    /// <para>
    /// Báo TOÀN BỘ vi phạm một lần chứ không dừng ở cái đầu tiên: soạn dữ liệu là việc lặp, và bắt
    /// người soạn chạy lại một lần cho mỗi lỗi chính tả là cách chắc chắn nhất để họ bỏ kiểm.
    /// </para>
    /// </summary>
    public sealed class JsonGraphDataSource : IGraphDataSource
    {
        private readonly IOntology _ontology;
        private readonly ICorpusIngestionService _corpus;
        private readonly GraphConfig _config;

        public JsonGraphDataSource(IOntology ontology,
                                   ICorpusIngestionService corpus,
                                   IOptions<GraphConfig> options)
        {
            _ontology = ontology;
            _corpus = corpus;
            _config = options.Value;
        }

        public async Task<GraphData> LoadAsync(CancellationToken cancellationToken = default)
        {
            var directory = ContentFilePath.Resolve(_config.DataPath);

            var entities = Read<List<EntityDocument>>(Path.Combine(directory, GraphDataFiles.Entities));
            var relationships = Read<List<RelationshipDocument>>(Path.Combine(directory, RelationshipsFile));

            // Tập mã chunk hợp lệ lấy từ ĐÚNG đường sinh mã của ứng dụng, không phải từ một bản tự
            // đếm lại: nếu hai bên đếm khác nhau thì phép kiểm này vô nghĩa.
            var preview = await _corpus.PreviewAsync(cancellationToken: cancellationToken);

            var validCodes = preview
                .SelectMany(document => document.Chunks)
                .Select(chunk => chunk.ChunkCode)
                .Where(code => code.Length > 0)
                .ToHashSet(StringComparer.Ordinal);

            var data = new GraphData(
                entities.Select(entity => new GraphEntityData(
                    entity.Ten,
                    entity.Nhan ?? new List<string>(),
                    entity.BiDanh ?? new List<string>(),
                    entity.NguonChunk ?? new List<string>(),
                    ReadProperties(entity.ThuocTinh))).ToList(),
                relationships.Select(relationship => new GraphRelationshipData(
                    relationship.Tu,
                    relationship.Loai,
                    relationship.Den,
                    relationship.TrangThai,
                    relationship.PhuDinh ?? false,
                    relationship.NguonChunk ?? new List<string>(),
                    ReadProperties(relationship.ThuocTinh))).ToList());

            Validate(data, validCodes);

            return data;
        }

        private void Validate(GraphData data, IReadOnlySet<string> validCodes)
        {
            var violations = new List<string>();
            var labelsByName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            // 1. Nhãn thuộc ontology, và tên thực thể không trùng nhau.
            foreach (var entity in data.Entities)
            {
                if (!labelsByName.TryAdd(entity.Name, entity.Labels))
                    violations.Add($"Thực thể trùng tên: '{entity.Name}'.");

                foreach (var label in entity.Labels.Where(label => !_ontology.Labels.Contains(label)))
                    violations.Add($"Thực thể '{entity.Name}' mang nhãn '{label}' không có trong ontology.");
            }

            var symmetricSeen = new HashSet<(string Type, string A, string B)>();

            foreach (var relationship in data.Relationships)
            {
                var label = $"'{relationship.From}' -{relationship.Type}-> '{relationship.To}'";

                // 2. Loại quan hệ thuộc ontology.
                if (!_ontology.Relations.TryGetValue(relationship.Type, out var spec))
                {
                    violations.Add($"{label}: loại quan hệ không có trong ontology.");
                    continue;
                }

                // 3. Hai đầu mút phải TỒN TẠI trong entities.json. Đây là chỗ bắt bẫy dấu tiếng Việt:
                // viết 'Phap y' thay vì 'Pháp y' thì Neo4j lặng lẽ tạo node thứ hai, NPC đó mất sạch
                // tri thức domain, mà Cypher vẫn chạy không một lời cảnh báo.
                var fromLabels = labelsByName.GetValueOrDefault(relationship.From);
                var toLabels = labelsByName.GetValueOrDefault(relationship.To);

                if (fromLabels is null)
                    violations.Add($"{label}: đầu nguồn không có trong entities.json.");

                if (toLabels is null)
                    violations.Add($"{label}: đầu đích không có trong entities.json.");

                // 4. trang_thai thuộc ontology.
                if (!_ontology.Statuses.ContainsKey(relationship.Status))
                    violations.Add($"{label}: trang_thai '{relationship.Status}' không có trong ontology.");

                // 5. Cặp nhãn hợp lệ.
                if (fromLabels is not null && toLabels is not null &&
                    !spec.ValidPairs.Any(pair => fromLabels.Contains(pair.From) && toLabels.Contains(pair.To)))
                {
                    violations.Add($"{label}: cặp nhãn ({string.Join('/', fromLabels)} → " +
                                   $"{string.Join('/', toLabels)}) không nằm trong cap_hop_le.");
                }

                // 6. Loại đối xứng không được khai hai chiều. So bằng cặp KHÔNG THỨ TỰ, vì khai
                // ngược chiều cũng là cùng một sự thật.
                if (spec.Symmetric)
                {
                    var ordered = string.CompareOrdinal(relationship.From, relationship.To) <= 0
                        ? (spec.Name, relationship.From, relationship.To)
                        : (spec.Name, relationship.To, relationship.From);

                    if (!symmetricSeen.Add(ordered))
                        violations.Add($"{label}: loại đối xứng bị khai hai chiều; chỉ khai MỘT chiều, bộ nạp tự phát chiều kia.");
                }

                // 7. Mã chunk có thật.
                violations.AddRange(relationship.ChunkCodes
                    .Where(code => !validCodes.Contains(code))
                    .Select(code => $"{label}: nguon_chunk '{code}' không tồn tại trong corpus."));
            }

            violations.AddRange(data.Entities
                .SelectMany(entity => entity.ChunkCodes.Select(code => (entity.Name, Code: code)))
                .Where(entry => !validCodes.Contains(entry.Code))
                .Select(entry => $"Thực thể '{entry.Name}': nguon_chunk '{entry.Code}' không tồn tại trong corpus."));

            // 8. Cặp hợp lệ của loại đối xứng phải ĐÓNG DƯỚI PHÉP ĐẢO, vì bộ nạp sẽ phát cạnh ngược
            // và cạnh ngược cũng phải hợp lệ. Kiểm ontology chứ không kiểm dữ liệu — một khai báo
            // không đóng sẽ chỉ lộ ra khi tình cờ có một cạnh rơi đúng vào cặp thiếu.
            foreach (var spec in _ontology.Relations.Values.Where(relation => relation.Symmetric))
            {
                var pairs = spec.ValidPairs.Select(pair => (pair.From, pair.To)).ToHashSet();

                violations.AddRange(pairs
                    .Where(pair => !pairs.Contains((pair.To, pair.From)))
                    .Select(pair => $"Ontology: {spec.Name} đối xứng nhưng cap_hop_le thiếu chiều ngược của ({pair.From}, {pair.To})."));
            }

            if (violations.Count > 0)
                throw new GraphDataInvalidException(InvalidMessage, violations);
        }

        private static T Read<T>(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), SerializerOptions)
                   ?? throw new InvalidOperationException(path);
        }

        private const string RelationshipsFile = "relationships.json";

        private const string InvalidMessage =
            "Dữ liệu đồ thị không hợp lệ. Không ghi gì lên Neo4j — ghi một nửa rồi dừng sẽ để lại " +
            "một đồ thị không ai biết đang ở trạng thái nào, mà bộ nạp thì không có đường xóa.";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        /// <summary>
        /// Chuyển <c>thuoc_tinh</c> sang kiểu mà driver Neo4j nhận được, và CHỈ nhận hai dạng:
        /// chuỗi, hoặc mảng chuỗi.
        /// <para>
        /// Số và boolean bị ép về chuỗi có chủ đích: chúng chưa từng xuất hiện trong dữ liệu, và
        /// nhận bừa một kiểu thứ ba nghĩa là cùng một khóa có thể mang <c>4</c> ở dòng này và
        /// <c>"4"</c> ở dòng kia — Neo4j chấp nhận cả hai rồi mọi so sánh sau đó đều hụt.
        /// Dạng khác (object, mảng lồng) thì BỎ QUA thay vì ném: đây là dữ liệu trang trí cho
        /// prompt, không đáng để một khóa lạ chặn cả lượt nạp.
        /// </para>
        /// </summary>
        private static IReadOnlyDictionary<string, object> ReadProperties(Dictionary<string, JsonElement>? source)
        {
            var properties = new Dictionary<string, object>(StringComparer.Ordinal);

            if (source is null)
                return properties;

            foreach (var (key, value) in source)
            {
                switch (value.ValueKind)
                {
                    case JsonValueKind.String:
                        properties[key] = value.GetString() ?? string.Empty;
                        break;

                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        properties[key] = value.ToString();
                        break;

                    case JsonValueKind.Array:
                        properties[key] = value.EnumerateArray()
                            .Where(item => item.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                            .Select(item => item.ValueKind == JsonValueKind.String
                                ? item.GetString() ?? string.Empty
                                : item.ToString())
                            .ToList();
                        break;
                }
            }

            return properties;
        }

        private sealed record EntityDocument(
            [property: JsonPropertyName("ten")] string Ten,
            [property: JsonPropertyName("nhan")] List<string>? Nhan,
            [property: JsonPropertyName("bi_danh")] List<string>? BiDanh,
            [property: JsonPropertyName("nguon_chunk")] List<string>? NguonChunk,
            [property: JsonPropertyName("thuoc_tinh")] Dictionary<string, JsonElement>? ThuocTinh);

        private sealed record RelationshipDocument(
            [property: JsonPropertyName("tu")] string Tu,
            [property: JsonPropertyName("loai")] string Loai,
            [property: JsonPropertyName("den")] string Den,
            [property: JsonPropertyName("trang_thai")] string TrangThai,
            [property: JsonPropertyName("phu_dinh")] bool? PhuDinh,
            [property: JsonPropertyName("nguon_chunk")] List<string>? NguonChunk,
            [property: JsonPropertyName("thuoc_tinh")] Dictionary<string, JsonElement>? ThuocTinh);
    }
}
