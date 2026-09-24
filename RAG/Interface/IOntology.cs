namespace RAG.Interface
{
    /// <summary>Một cặp nhãn hợp lệ cho hai đầu của một loại quan hệ.</summary>
    public sealed record LabelPair(string From, string To);

    /// <summary>Khai báo của một loại quan hệ.</summary>
    /// <param name="Symmetric">
    /// Chiều KHÔNG mang nghĩa. Nguồn dữ liệu khai một chiều, bộ nạp phát hai chiều để truy vấn đi từ
    /// phía nào cũng thấy — và vì thế truy vấn vô hướng phải khử trùng, nếu không cùng một sự thật
    /// vào prompt hai lần.
    /// </param>
    /// <param name="Reasoning">
    /// Cạnh mang lập luận (ủng hộ, mâu thuẫn, quy kết lời khai). Kéo theo hai hệ quả trong truy vấn:
    /// được xếp lên trước trong cùng mức độ tin cậy, và bộ lọc quyền dùng <c>all</c> thay vì
    /// <c>any</c> — một suy luận chỉ có nghĩa với NPC đọc được MỌI dòng mà nó tựa lên.
    /// </param>
    /// <param name="NegatedDisplayName">
    /// Nhãn dùng khi cạnh mang <c>phu_dinh</c>. Rỗng nghĩa là loại này chưa từng xuất hiện ở dạng
    /// phủ định trong dữ liệu; lúc đó <see cref="IOntology.DisplayRelation(string, bool)"/> ghép
    /// tiền tố phủ định vào <see cref="DisplayName"/> thay vì âm thầm render thành câu khẳng định.
    /// </param>
    public sealed record RelationSpec(string Name,
                                      bool Symmetric,
                                      bool Reasoning,
                                      string DisplayName,
                                      string? NegatedDisplayName,
                                      IReadOnlyList<LabelPair> ValidPairs);

    /// <summary>Khai báo của một giá trị độ tin cậy.</summary>
    /// <param name="Priority">Càng nhỏ càng đáng tin; quyết định thứ tự cạnh đi vào prompt.</param>
    /// <param name="AllowedInPrompt">
    /// Có được đưa vào prompt hay không. Mặc định mọi giá trị đều được, KỂ CẢ tin đồn — tin đồn
    /// chính là toàn bộ tri thức của mấy NPC hàng xóm, lọc bỏ thì họ không còn gì để nói. Thứ ngăn
    /// mô hình trình bày tin đồn như kết luận là <see cref="DisplayName"/> đi kèm từng dòng, không
    /// phải việc loại bỏ.
    /// </param>
    public sealed record StatusSpec(string Name, int Priority, bool AllowedInPrompt, string DisplayName);

    /// <summary>
    /// Từ vựng của đồ thị, đọc từ <c>ontology.json</c>.
    /// <para>
    /// Tồn tại để giữ đúng một lời hứa: KHÔNG một tên nhãn, tên loại quan hệ hay giá trị
    /// <c>trang_thai</c> nào được viết vào code C#. Những gì interface này phơi ra là đúng những gì
    /// câu Cypher cần nhận dưới dạng THAM SỐ — nhờ vậy soạn lại ontology chỉ là sửa một file JSON:
    /// không build lại, không sửa Cypher, không sửa prompt.
    /// </para>
    /// </summary>
    public interface IOntology
    {
        /// <summary>Nhãn nền có trên mọi node. Truy vấn mở rộng bắt đầu từ đây.</summary>
        string BaseLabel { get; }

        /// <summary>Nhãn của node mang phân quyền. Chỉ node này có thuộc tính <c>duoc_biet</c>.</summary>
        string NpcLabel { get; }

        IReadOnlyList<string> Labels { get; }

        IReadOnlyDictionary<string, RelationSpec> Relations { get; }

        IReadOnlyDictionary<string, StatusSpec> Statuses { get; }

        /// <summary>Các <c>trang_thai</c> được phép vào prompt, dựng sẵn để làm tham số Cypher.</summary>
        IReadOnlyList<string> AllowedStatuses { get; }

        /// <summary>Ánh xạ <c>trang_thai</c> → thứ tự ưu tiên, dựng sẵn để làm tham số Cypher.</summary>
        IReadOnlyDictionary<string, int> StatusPriority { get; }

        IReadOnlyList<string> SymmetricRelations { get; }

        IReadOnlyList<string> ReasoningRelations { get; }

        /// <summary>Nhãn tiếng Việt của một loại quan hệ. Không khai báo thì trả lại tên gốc.</summary>
        string DisplayRelation(string relation);

        /// <summary>
        /// Nhãn hiển thị có tính tới phủ định.
        /// <para>
        /// Bắt buộc dùng bản này ở mọi chỗ render cạnh vào prompt. Dùng bản một tham số cho một cạnh
        /// mang <c>phu_dinh</c> là biến "trên tay Danie KHÔNG có dấu hiệu cầm súng" thành lời khẳng
        /// định ngược hẳn, mang nhãn "đã xác nhận" — và NPC pháp y sẽ nói với người chơi đúng điều
        /// trái với kết quả khám nghiệm của chính mình.
        /// </para>
        /// </summary>
        string DisplayRelation(string relation, bool negated);

        /// <summary>Nhãn tiếng Việt của một <c>trang_thai</c>. Không khai báo thì trả lại tên gốc.</summary>
        string DisplayStatus(string status);
    }
}
