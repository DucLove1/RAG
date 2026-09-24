using System.Text;

namespace RAG.Interface
{
    /// <summary>
    /// Danh mục thực thể mà MỘT NPC được biết, kèm phép đối chiếu tên do LLM trả về về tên chuẩn.
    /// <para>
    /// Danh mục là theo NPC chứ không dùng chung: Edward đọc được hồ sơ pháp y nên biết "Khẩu súng",
    /// Jack thì không. Vì vậy <see cref="Resolve"/> nằm trên chính danh mục của một NPC — một hàm
    /// <c>Resolve(tên)</c> không kèm NPC thì không biết phải tra quyền của ai.
    /// </para>
    /// </summary>
    public sealed class NpcEntityCatalog
    {
        private readonly IReadOnlyDictionary<string, string> _lookup;

        private NpcEntityCatalog(IReadOnlyList<GraphCatalogEntry> entries, bool failed)
        {
            Entries = entries;
            Failed = failed;
            SelfName = entries.FirstOrDefault(entry => entry.IsSelf)?.Name;
            (_lookup, AmbiguousKeys) = BuildLookup(entries);
        }

        public static NpcEntityCatalog Empty { get; } = new(Array.Empty<GraphCatalogEntry>(), failed: false);

        /// <summary>Không lấy được danh mục vì sự cố. Khác <see cref="Empty"/>: đây là suy biến.</summary>
        public static NpcEntityCatalog Unavailable { get; } = new(Array.Empty<GraphCatalogEntry>(), failed: true);

        public static NpcEntityCatalog From(IReadOnlyList<GraphCatalogEntry> entries) =>
            entries.Count == 0 ? Empty : new NpcEntityCatalog(entries, failed: false);

        public IReadOnlyList<GraphCatalogEntry> Entries { get; }

        public bool Failed { get; }

        public bool IsEmpty => Entries.Count == 0;

        /// <summary>Tên chuẩn của chính NPC, kể cả khi endpoint được gọi bằng bí danh.</summary>
        public string? SelfName { get; }

        /// <summary>
        /// Các cách gọi trỏ tới hơn một thực thể nên đã bị bỏ khỏi phép đối chiếu. Phơi ra để tầng
        /// gọi ghi log và để endpoint chẩn đoán nhìn thấy — lỗi dữ liệu kiểu này không làm gì hỏng
        /// ồn ào, nó chỉ lặng lẽ làm một bí danh không bao giờ khớp.
        /// </summary>
        public IReadOnlyList<string> AmbiguousKeys { get; }

        /// <summary>
        /// Tên hoặc bí danh → tên CHUẨN đúng từng ký tự như trong đồ thị (Cypher khớp <c>ten</c> chính
        /// xác), hoặc <c>null</c> nếu NPC không biết thực thể nào như vậy.
        /// </summary>
        public string? Resolve(string? mention)
        {
            if (string.IsNullOrWhiteSpace(mention))
                return null;

            return _lookup.TryGetValue(Key(mention), out var name) ? name : null;
        }

        /// <summary>
        /// Khóa so khớp: bỏ khoảng trắng hai đầu và chuẩn hóa Unicode về dạng dựng sẵn (FormC). Tiếng
        /// Việt có hai cách mã hóa cùng một chữ có dấu; LLM trả một kiểu, dữ liệu lưu kiểu kia thì hai
        /// chuỗi nhìn giống hệt mà so vẫn khác. Không phân biệt hoa thường nằm ở bộ so sánh của từ điển.
        /// </summary>
        private static string Key(string value) => value.Trim().Normalize(NormalizationForm.FormC);

        /// <summary>
        /// Luật không phụ thuộc thứ tự dòng: tên chuẩn luôn thắng bí danh của thực thể khác; một cách
        /// gọi trỏ tới hai thực thể trở lên thì bị bỏ hẳn, vì chọn bừa một bên là gieo sai hạt giống.
        /// </summary>
        private static (IReadOnlyDictionary<string, string> Lookup, IReadOnlyList<string> Ambiguous) BuildLookup(
            IReadOnlyList<GraphCatalogEntry> entries)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var key = Key(entry.Name);

                if (key.Length > 0 && !names.TryAdd(key, entry.Name) &&
                    !string.Equals(names[key], entry.Name, StringComparison.Ordinal))
                {
                    ambiguous.Add(key);
                }
            }

            foreach (var entry in entries)
            {
                foreach (var alias in entry.Aliases)
                {
                    var key = Key(alias);

                    if (key.Length == 0 || names.ContainsKey(key))
                        continue;

                    if (!aliases.TryAdd(key, entry.Name) &&
                        !string.Equals(aliases[key], entry.Name, StringComparison.Ordinal))
                    {
                        ambiguous.Add(key);
                    }
                }
            }

            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, name) in names.Concat(aliases))
            {
                if (!ambiguous.Contains(key))
                    lookup[key] = name;
            }

            return (lookup, ambiguous.OrderBy(key => key, StringComparer.Ordinal).ToList());
        }
    }

    /// <summary>
    /// Nguồn danh mục thực thể theo NPC. Tách khỏi <see cref="IGraphStore"/> để có chỗ đặt cache:
    /// danh mục chỉ đổi khi nạp lại đồ thị, nhưng được đọc ở MỌI câu hỏi trượt cache câu trả lời.
    /// <para>
    /// Như <see cref="IGraphStore"/>: KHÔNG ném, trừ khi token của caller bị hủy. Sự cố trả
    /// <see cref="NpcEntityCatalog.Unavailable"/>.
    /// </para>
    /// </summary>
    public interface IGraphEntityCatalog
    {
        Task<NpcEntityCatalog> GetAsync(string npcName, CancellationToken cancellationToken = default);
    }
}
