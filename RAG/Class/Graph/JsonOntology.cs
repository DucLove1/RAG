using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Đọc <c>ontology.json</c> một lần lúc khởi động.
    /// <para>
    /// Đọc trong constructor và ném nếu hỏng, thay vì đọc lười lúc chạy: ontology sai là lỗi TRIỂN
    /// KHAI, và một app khởi động được rồi mới chết ở câu hỏi đầu tiên của người chơi thì tốn của
    /// người vận hành một vòng chẩn đoán hoàn toàn không cần thiết. Cùng lý do với
    /// <c>ValidateOnStart</c> của các options.
    /// </para>
    /// <para>
    /// <see cref="Fingerprint"/> đi vào vân tay phân vùng của cache câu trả lời, nên sửa ontology là
    /// tự động làm nguội cache — không có nó thì câu trả lời sinh bằng từ vựng cũ vẫn được phục vụ
    /// nguyên văn cho tới khi hết hạn.
    /// </para>
    /// </summary>
    public sealed class JsonOntology : IOntology
    {
        private readonly Dictionary<string, RelationSpec> _relations;
        private readonly Dictionary<string, StatusSpec> _statuses;

        public JsonOntology(IOptions<GraphConfig> options)
        {
            var path = Path.Combine(ContentFilePath.Resolve(options.Value.DataPath), FileName);

            if (!File.Exists(path))
                throw new FileNotFoundException(MissingMessage, path);

            var raw = File.ReadAllText(path);

            var document = JsonSerializer.Deserialize<OntologyDocument>(raw, SerializerOptions)
                           ?? throw new InvalidOperationException(MissingMessage);

            Version = document.PhienBan;
            Fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(raw)))[..16];

            Labels = document.Nhan.Select(label => label.Ten).ToList();

            // Nhãn nền và nhãn NPC nhận ra bằng CỜ chứ không bằng tên: đây chính là chỗ mà một dòng
            // `if (ten == "ThucThe")` sẽ lén đưa từ vựng của vụ án vào code C#.
            BaseLabel = Single(document.Nhan.Where(label => label.Goc), nameof(LabelDocument.Goc));
            NpcLabel = Single(document.Nhan.Where(label => label.Npc), nameof(LabelDocument.Npc));

            _statuses = document.TrangThai.ToDictionary(
                status => status.Ten,
                status => new StatusSpec(status.Ten, status.UuTien, status.ChoPhepVaoPrompt, status.NhanHienThi ?? status.Ten),
                StringComparer.Ordinal);

            _relations = document.QuanHe.ToDictionary(
                relation => relation.Loai,
                relation => new RelationSpec(
                    relation.Loai,
                    relation.DoiXung,
                    relation.MangLapLuan,
                    relation.NhanHienThi ?? relation.Loai,
                    relation.NhanHienThiPhuDinh,
                    relation.CapHopLe.Select(pair => new LabelPair(pair[0], pair[1])).ToList()),
                StringComparer.Ordinal);

            // Dựng sẵn ở đây thay vì tính lại mỗi truy vấn: chúng đi thẳng vào tham số Cypher trên
            // đường nóng, và chúng không bao giờ đổi trong một vòng đời tiến trình.
            AllowedStatuses = _statuses.Values.Where(status => status.AllowedInPrompt)
                                              .OrderBy(status => status.Priority)
                                              .Select(status => status.Name)
                                              .ToList();

            StatusPriority = _statuses.Values.ToDictionary(status => status.Name, status => status.Priority, StringComparer.Ordinal);

            SymmetricRelations = _relations.Values.Where(relation => relation.Symmetric).Select(relation => relation.Name).ToList();
            ReasoningRelations = _relations.Values.Where(relation => relation.Reasoning).Select(relation => relation.Name).ToList();
        }

        public string Version { get; }

        public string Fingerprint { get; }

        public string BaseLabel { get; }

        public string NpcLabel { get; }

        public IReadOnlyList<string> Labels { get; }

        public IReadOnlyDictionary<string, RelationSpec> Relations => _relations;

        public IReadOnlyDictionary<string, StatusSpec> Statuses => _statuses;

        public IReadOnlyList<string> AllowedStatuses { get; }

        public IReadOnlyDictionary<string, int> StatusPriority { get; }

        public IReadOnlyList<string> SymmetricRelations { get; }

        public IReadOnlyList<string> ReasoningRelations { get; }

        /// <summary>
        /// Không khai báo nhãn hiển thị thì rơi về tên gốc. KHÔNG ném: thêm một loại quan hệ vào
        /// ontology mà quên đặt nhãn tiếng Việt chỉ nên làm prompt xấu đi một dòng, không được làm
        /// sập một câu hỏi của người chơi.
        /// </summary>
        /// <summary>Tiền tố dùng khi một loại quan hệ chưa có nhãn phủ định soạn sẵn.</summary>
        private const string NegationPrefix = "không ";

        public string DisplayRelation(string relation) =>
            _relations.TryGetValue(relation, out var spec) ? spec.DisplayName : relation;

        /// <summary>
        /// Thiếu <c>nhan_hien_thi_phu_dinh</c> thì ghép tiền tố "không " vào nhãn khẳng định. Không
        /// đẹp bằng một nhãn soạn tay, nhưng nó giữ đúng NGHĨA — còn rơi về nhãn khẳng định thì câu
        /// sai hẳn mà không gì báo.
        /// </summary>
        public string DisplayRelation(string relation, bool negated)
        {
            if (!negated)
                return DisplayRelation(relation);

            if (!_relations.TryGetValue(relation, out var spec))
                return NegationPrefix + relation;

            return string.IsNullOrWhiteSpace(spec.NegatedDisplayName)
                ? NegationPrefix + spec.DisplayName
                : spec.NegatedDisplayName;
        }

        public string DisplayStatus(string status) =>
            _statuses.TryGetValue(status, out var spec) ? spec.DisplayName : status;

        private static string Single(IEnumerable<LabelDocument> matches, string flag)
        {
            var found = matches.ToList();

            return found.Count == 1
                ? found[0].Ten
                : throw new InvalidOperationException(string.Format(FlagMessage, flag, found.Count));
        }

        private const string FileName = "ontology.json";

        private const string MissingMessage =
            "Không đọc được ontology.json. Đây là từ vựng duy nhất của đồ thị: thiếu nó thì mọi câu " +
            "Cypher đều thiếu tham số và local search sẽ trả về rỗng cho 100% câu hỏi.";

        private const string FlagMessage =
            "ontology.json phải có ĐÚNG MỘT nhãn mang cờ '{0}', đang có {1}. Cờ này là cách duy nhất " +
            "để code nhận ra nhãn đó mà không phải viết tên nó vào C#.";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private sealed record OntologyDocument(
            [property: JsonPropertyName("phien_ban")] string PhienBan,
            [property: JsonPropertyName("nhan")] List<LabelDocument> Nhan,
            [property: JsonPropertyName("trang_thai")] List<StatusDocument> TrangThai,
            [property: JsonPropertyName("quan_he")] List<RelationDocument> QuanHe);

        private sealed record LabelDocument(
            [property: JsonPropertyName("ten")] string Ten,
            [property: JsonPropertyName("goc")] bool Goc,
            [property: JsonPropertyName("npc")] bool Npc);

        private sealed record StatusDocument(
            [property: JsonPropertyName("ten")] string Ten,
            [property: JsonPropertyName("uu_tien")] int UuTien,
            [property: JsonPropertyName("cho_phep_vao_prompt")] bool ChoPhepVaoPrompt,
            [property: JsonPropertyName("nhan_hien_thi")] string? NhanHienThi);

        private sealed record RelationDocument(
            [property: JsonPropertyName("loai")] string Loai,
            [property: JsonPropertyName("doi_xung")] bool DoiXung,
            [property: JsonPropertyName("mang_lap_luan")] bool MangLapLuan,
            [property: JsonPropertyName("nhan_hien_thi")] string? NhanHienThi,
            [property: JsonPropertyName("nhan_hien_thi_phu_dinh")] string? NhanHienThiPhuDinh,
            [property: JsonPropertyName("cap_hop_le")] List<List<string>> CapHopLe);
    }
}
