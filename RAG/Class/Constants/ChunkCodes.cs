namespace RAG.Class.Constants
{
    /// <summary>
    /// Cú pháp mã chunk <c>&lt;doc_id&gt;#L&lt;n&gt;</c> — khóa ghép DUY NHẤT giữa nhánh vector và
    /// nhánh đồ thị.
    /// <para>
    /// Là hằng GIAO THỨC chứ không phải núm cấu hình, nên nằm ở đây chứ không trong
    /// <c>appsettings.json</c>: nó là hợp đồng với dữ liệu đồ thị viết tay (mọi node và cạnh mang
    /// <c>nguon_chunk</c> theo đúng dạng này) và với công thức sinh mã đã công bố trong HUONG_DAN.md của
    /// kho tri thức. Đổi nó là làm hỏng toàn bộ đồ thị, không phải là tinh chỉnh một tham số.
    /// </para>
    /// </summary>
    public static class ChunkCodes
    {
        /// <summary>Dấu ngăn giữa mã tài liệu và số thứ tự dòng.</summary>
        public const string Separator = "#L";

        /// <summary>Ký tự mở đầu dấu ngăn, dùng để tách phần mã tài liệu ở cả Cypher lẫn C#.</summary>
        public const char DocIdTerminator = '#';

        /// <summary>Số thứ tự dòng bắt đầu từ 1, theo đúng công thức trong HUONG_DAN.md.</summary>
        public const int FirstOrdinal = 1;

        public static string Build(string docId, int ordinal) => $"{docId}{Separator}{ordinal}";

        /// <summary>
        /// Phần trước dấu <c>#</c>. Đây là thứ duy nhất đối chiếu được với <c>duoc_biet</c> của NPC
        /// trong đồ thị — danh sách quyền là danh sách MÃ TÀI LIỆU, không phải danh sách dòng.
        /// </summary>
        public static string DocId(string code)
        {
            var separator = code.IndexOf(DocIdTerminator);

            return separator < 0 ? code : code[..separator];
        }

        public static bool TryParse(string? code, out string docId, out int ordinal)
        {
            docId = string.Empty;
            ordinal = 0;

            if (string.IsNullOrWhiteSpace(code))
                return false;

            var separator = code.IndexOf(Separator, StringComparison.Ordinal);

            if (separator <= 0)
                return false;

            if (!int.TryParse(code[(separator + Separator.Length)..], out ordinal) || ordinal < FirstOrdinal)
                return false;

            docId = code[..separator];

            return true;
        }

        /// <summary>
        /// Các dòng liền kề trong cùng tài liệu, tính bằng SỐ HỌC.
        /// <para>
        /// Cố ý không hỏi đồ thị: đồ thị không chứa văn bản và không chứa mã dòng nào ngoài những mã
        /// đã gắn trên node và cạnh, nên nó không có gì để trả lời về chuyện "dòng bên cạnh là
        /// dòng nào". Đây là việc của nhánh RAG, và nó chỉ là phép cộng trừ.
        /// </para>
        /// <para>
        /// Mã sinh ra có thể KHÔNG tồn tại (dòng 0, hoặc quá dòng cuối của tài liệu). Đó là chuyện
        /// bình thường: bước tra văn bản chỉ đơn giản không tìm thấy và bỏ qua, rẻ hơn nhiều so với
        /// việc hỏi trước xem tài liệu có bao nhiêu dòng.
        /// </para>
        /// </summary>
        public static IEnumerable<string> Neighbours(string code, int radius)
        {
            if (radius <= 0 || !TryParse(code, out var docId, out var ordinal))
                yield break;

            for (var offset = -radius; offset <= radius; offset++)
            {
                if (offset == 0)
                    continue;

                var neighbour = ordinal + offset;

                if (neighbour >= FirstOrdinal)
                    yield return Build(docId, neighbour);
            }
        }
    }
}
