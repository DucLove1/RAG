using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Suy phân quyền tri thức từ front matter của corpus. Hai luật, hết:
    /// <list type="number">
    /// <item>Tài liệu <c>loai: boi_canh</c> thuộc về MỌI NPC — nó là dữ kiện nền.</item>
    /// <item>Mọi tài liệu khác thuộc về đúng những cái tên liệt kê trong <c>nguon</c>.</item>
    /// </list>
    /// <para>
    /// Danh sách "mọi NPC" dựng bằng hợp của <c>nguon</c> trên các tài liệu KHÔNG phải bối cảnh.
    /// Vì thế lớp này phải nhìn cả tập tài liệu một lượt — và vì thế phân quyền không thể tính được
    /// khi nạp lẻ từng file. Đó là một lý do thật để có endpoint nạp cả thư mục.
    /// </para>
    /// <para>
    /// Suy ra từ chính file thay vì chép tay một bảng: bảng chép tay là nguồn sự thật thứ hai, và
    /// một dòng gõ sai trong đó sẽ lấy mất tri thức của một NPC hoặc cho NPC đó biết thứ không được
    /// biết — cả hai đều im lặng.
    /// </para>
    /// <para>
    /// Tên trong <c>nguon</c> là VAI TRÒ ("Pháp y"); mọi tên ra khỏi lớp này đã qua
    /// <see cref="INpcNameResolver"/> thành TÊN RIÊNG ("Edward"), để payload Qdrant và
    /// <c>duoc_biet</c> của đồ thị gọi NPC giống hệt field <c>npcName</c> của endpoint.
    /// </para>
    /// </summary>
    public sealed class FrontMatterAccessPolicy : IAccessPolicy
    {
        private readonly INpcNameResolver _npcNames;

        public FrontMatterAccessPolicy(INpcNameResolver npcNames)
        {
            _npcNames = npcNames;
        }

        public IReadOnlyList<DocumentAccess> Resolve(IReadOnlyList<IReadOnlyDictionary<string, string>> documents)
        {
            // Giữ thứ tự xuất hiện thay vì HashSet: danh sách NPC đi thẳng vào payload và vào dữ
            // liệu đồ thị, nên nó phải ổn định giữa hai lần nạp. Thứ tự lung tung làm hai lần nạp
            // cùng một corpus cho ra payload khác nhau và mọi phép so sánh mất nghĩa.
            var roster = new List<string>();

            foreach (var document in documents)
            {
                if (IsBackground(document))
                    continue;

                foreach (var npc in SourceNames(document))
                {
                    if (!roster.Contains(npc, StringComparer.Ordinal))
                        roster.Add(npc);
                }
            }

            var access = new List<DocumentAccess>(documents.Count);

            foreach (var document in documents)
            {
                var docId = document.TryGetValue(FrontMatterFields.DocId, out var id) ? id : string.Empty;

                access.Add(new DocumentAccess(
                    docId,
                    IsBackground(document) ? roster : SourceNames(document)));
            }

            return access;
        }

        private static bool IsBackground(IReadOnlyDictionary<string, string> document) =>
            document.TryGetValue(FrontMatterFields.Kind, out var kind) &&
            string.Equals(kind, DocumentKinds.Background, StringComparison.Ordinal);

        private List<string> SourceNames(IReadOnlyDictionary<string, string> document)
        {
            if (!document.TryGetValue(FrontMatterFields.Source, out var source) || string.IsNullOrWhiteSpace(source))
                return new List<string>();

            return source.Split(',')
                         .Select(name => name.Trim())
                         .Where(name => name.Length > 0)
                         .Select(_npcNames.Resolve)
                         .ToList();
        }
    }
}
