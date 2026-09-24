using System.ComponentModel.DataAnnotations;
using RAG.Class.Constants;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình tầng đồ thị tri thức (GraphRAG).
    /// <para>
    /// Ở đây chỉ có HÀNH VI: bật/tắt, chế độ, ngân sách, đường dẫn dữ liệu. Toàn bộ TỪ VỰNG của đồ
    /// thị — tên nhãn, tên loại quan hệ, giá trị <c>trang_thai</c>, loại nào đối xứng, loại nào mang
    /// lập luận, nhãn hiển thị tiếng Việt — nằm trong <c>ontology.json</c> và đi vào Cypher dưới
    /// dạng THAM SỐ. Nhờ vậy viết lại ontology chỉ là sửa một file JSON: không build lại, không sửa
    /// Cypher, không sửa prompt. Thêm một khóa kiểu <c>Graph:LoaiDoiXung</c> vào đây là phá đúng
    /// tính chất đó, vì từ vựng sẽ có hai nguồn sự thật.
    /// </para>
    /// </summary>
    public class GraphConfig
    {
        public const string SectionName = "Graph";

        /// <summary>
        /// Bật/tắt. Mặc định TẮT: khi tắt thì Null Object được đăng ký, nên đường trả lời không hề
        /// biết tầng này có tồn tại, không cần một biến Neo4j nào trong <c>.env</c>, và prompt sinh
        /// ra giống hệt từng byte so với trước khi có tính năng.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Kiểu truy hồi trên đồ thị. Xem <see cref="GraphSearchMode"/>.</summary>
        public GraphSearchMode Mode { get; set; } = GraphSearchMode.Local;

        /// <summary>
        /// Phiên bản của DỮ LIỆU đồ thị, do người soạn tự tăng mỗi khi nội dung
        /// <c>entities.json</c>/<c>relationships.json</c> đổi.
        /// <para>
        /// Không chỉ để hiển thị: nó nằm trong vân tay phân vùng của cache câu trả lời. Nạp lại đồ
        /// thị với dữ liệu đã sửa mà không tăng số này thì mọi câu trả lời cũ vẫn được phục vụ
        /// nguyên văn cho tới khi hết hạn — và với một bài đo thì đó là số liệu sai.
        /// </para>
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string DataVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Thư mục chứa ontology và dữ liệu đồ thị, tương đối so với thư mục chạy. Giữ đúng bố cục
        /// đã công bố trong HUONG_DAN.md của kho tri thức.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string DataPath { get; set; } = "graphrag";

        public GraphSearchConfig Search { get; set; } = new();

        public GraphLoaderConfig Loader { get; set; } = new();

        public GraphSchemaConfig Schema { get; set; } = new();

        public GraphExtractionConfig Extraction { get; set; } = new();
    }

    /// <summary>Núm của bước truy hồi trên đồ thị và của việc chia ngân sách ngữ cảnh.</summary>
    public class GraphSearchConfig
    {
        public const string SectionName = GraphConfig.SectionName + ":Search";

        /// <summary>
        /// Số cạnh tối đa đưa vào prompt.
        /// <para>
        /// Mở rộng một bậc từ vài dòng hạt giống dễ ra 40+ cạnh, phần lớn là bối cảnh không liên
        /// quan tới câu đang hỏi. Mốc 10–20 là vùng vừa phải: đủ để NPC nhớ ra mối liên hệ, chưa đủ
        /// để chôn mất những dòng đáng trích dẫn.
        /// </para>
        /// </summary>
        [Range(1, 200)]
        public int MaxRelations { get; set; } = 16;

        /// <summary>
        /// Chỉ giữ quan hệ mang lập luận (<c>mang_lap_luan</c> trong ontology).
        /// <para>
        /// Mặc định TẮT, và đó là một quyết định chứ không phải giá trị cho có. Thứ tự sắp xếp đã
        /// đẩy trạng thái mạnh và quan hệ lập luận lên đầu rồi, nên lọc cứng thêm chỉ làm mất những
        /// quan hệ bối cảnh đã xác nhận mà đôi khi CHÍNH LÀ câu trả lời (ai có mặt ở đâu, lúc nào).
        /// Bật khi muốn đo riêng phần lập luận.
        /// </para>
        /// </summary>
        public bool ReasoningRelationsOnly { get; set; } = false;

        /// <summary>
        /// Lấy thêm bao nhiêu dòng liền kề quanh mỗi mã chunk mà đồ thị trả về.
        /// <para>
        /// Tính bằng SỐ HỌC ở nhánh RAG (<c>doc#L(n-1)</c>, <c>doc#L(n+1)</c>), không hỏi đồ thị —
        /// đồ thị không chứa văn bản nên nó không có gì để trả lời về chuyện này.
        /// </para>
        /// </summary>
        [Range(0, 5)]
        public int AdjacentLineRadius { get; set; } = 1;

        /// <summary>
        /// Trần ký tự cho TOÀN BỘ phần ngữ cảnh của prompt (văn bản + khối đồ thị).
        /// <para>
        /// Đo bằng ký tự chứ không phải token vì dự án không có tokenizer nào, và một ngân sách ký
        /// tự thì trung thực về điều đó — một ngân sách "token" tính nhẩm sẽ sai lặng lẽ theo từng
        /// câu tiếng Việt có dấu.
        /// </para>
        /// </summary>
        [Range(500, 100000)]
        public int ContextBudgetChars { get; set; } = 6000;

        /// <summary>
        /// Phần ngân sách dành cho khối đồ thị.
        /// <para>
        /// Chia CỐ ĐỊNH là điểm mấu chốt: không có nó, một vùng lân cận dày đặc sẽ đẩy hết những
        /// dòng nguyên văn ra khỏi prompt, và mô hình còn lại một danh sách quan hệ mà nó không dẫn
        /// nguồn được.
        /// </para>
        /// </summary>
        [Range(0.0, 1.0)]
        public double GraphShareRatio { get; set; } = 0.30;

        /// <summary>
        /// Cho phép phần ngân sách đồ thị còn thừa chảy sang cho văn bản.
        /// <para>
        /// Mặc định KHÔNG, để độ dài prompt ổn định giữa các câu hỏi. Prompt dài ngắn thất thường
        /// làm hai lần chạy cùng một bộ câu hỏi không so được với nhau.
        /// </para>
        /// </summary>
        public bool AllowShareSpillover { get; set; } = false;
    }

    /// <summary>Núm của đường NẠP dữ liệu vào Neo4j. Toàn bộ là thao tác quản trị, không phải đường trả lời.</summary>
    public class GraphLoaderConfig
    {
        public const string SectionName = GraphConfig.SectionName + ":Loader";

        /// <summary>
        /// Mở các endpoint quản trị đồ thị. Mặc định TẮT, và ngay cả khi bật thì chúng vẫn chỉ sống
        /// trong môi trường Development — cùng cách bảo vệ với đường quản trị câu mẫu của router.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Tạo constraint và index lúc khởi động. Thao tác này idempotent và rẻ, nhưng constraint
        /// UNIQUE phải tồn tại TRƯỚC lần nạp đầu tiên: <c>MERGE</c> không có constraint vẫn có thể
        /// đẻ node trùng dưới đồng thời, và Neo4j từ chối thêm constraint sau khi đã có trùng.
        /// </summary>
        public bool EnsureIndexesOnStartup { get; set; } = true;

        /// <summary>
        /// Đối chiếu số đếm giữa file dữ liệu và database lúc khởi động rồi ghi log. Không sửa gì.
        /// Nhờ vậy một bản deploy bị lệch dữ liệu lộ ra ngay trong mười dòng log đầu.
        /// </summary>
        public bool VerifyOnStartup { get; set; } = true;

        /// <summary>
        /// Nạp dữ liệu lúc khởi động. Mặc định KHÔNG, có chủ đích: mỗi lần khởi động lại MERGE toàn
        /// bộ đồ thị vừa ồn vừa nguy hiểm — nó đẩy file đóng gói trong image đè lên bản đã sửa tay
        /// trực tiếp trên database. Nạp phải là một hành động có người bấm.
        /// </summary>
        public bool LoadOnStartup { get; set; } = false;
    }

    /// <summary>
    /// Tên của constraint và index trong Neo4j. Là DANH TÍNH đối tượng trong database chứ không phải
    /// từ vựng của vụ án, nên chúng nằm ở đây thay vì trong ontology: đổi tên một index là việc di
    /// trú schema, không phải việc soạn lại tri thức.
    /// </summary>
    public class GraphSchemaConfig
    {
        public const string SectionName = GraphConfig.SectionName + ":Schema";

        [Required(AllowEmptyStrings = false)]
        public string EntityNameConstraint { get; set; } = "thucthe_ten_unique";

        [Required(AllowEmptyStrings = false)]
        public string NpcNameIndex { get; set; } = "nhanvat_ten";

        [Required(AllowEmptyStrings = false)]
        public string FulltextNameIndex { get; set; } = "thucthe_fulltext";

        [Required(AllowEmptyStrings = false)]
        public string FulltextAliasIndex { get; set; } = "thucthe_bidanh";
    }

    /// <summary>
    /// Cấu hình bước LLM trích thực thể và ý định quan hệ từ câu hỏi — nguồn hạt giống của đồ thị.
    /// <para>
    /// LLM chỉ được CHỌN trong danh mục thực thể mà NPC được biết (lấy từ chính đồ thị) và trong
    /// danh mục loại quan hệ của ontology; mọi tên nó trả về đều bị đối chiếu lại, tên lạ bị bỏ.
    /// </para>
    /// </summary>
    public class GraphExtractionConfig : IValidatableObject
    {
        public const string SectionName = GraphConfig.SectionName + ":Extraction";

        /// <summary>
        /// Provider riêng, độc lập với provider trả lời — giống bộ chuẩn hóa và bộ định tuyến, để
        /// bước trích không đốt hạn mức của pool sinh câu trả lời.
        /// </summary>
        public LlmProviderKey Provider { get; set; } = LlmProviderKey.Gemini;

        /// <summary>Để trống thì dùng model mặc định của provider.</summary>
        public string? Model { get; set; }

        /// <summary>Câu dài hơn thì không trích (trả rỗng, không tính là suy biến).</summary>
        [Range(1, int.MaxValue)]
        public int MaxInputLength { get; set; } = 500;

        /// <summary>Số thực thể tối đa làm hạt giống. Cắt SAU khi đối chiếu với danh mục.</summary>
        [Range(1, 50)]
        public int MaxEntities { get; set; } = 5;

        /// <summary>Số loại quan hệ tối đa dùng để ƯU TIÊN cạnh (không lọc cứng).</summary>
        [Range(0, 50)]
        public int MaxRelationTypes { get; set; } = 4;

        /// <summary>
        /// Hạn chót riêng của lượt gọi LLM. Cần riêng vì timeout 30s của HttpClient quá dài cho một
        /// bước nằm trên đường nóng. Hết hạn thì HỦY hẳn lượt gọi HTTP (không chỉ thôi chờ), rồi trả
        /// kết quả suy biến để câu trả lời tụt về RAG thuần. Cận dưới là 1 để đặt được giá trị cực
        /// nhỏ khi kiểm đường suy biến.
        /// </summary>
        [Range(1, 600000)]
        public int TimeoutMs { get; set; } = 4000;

        /// <summary>
        /// Thời gian sống (tuyệt đối) của danh mục thực thể theo NPC trong RAM. Danh mục chỉ đổi khi
        /// nạp lại đồ thị, nên cache dài là an toàn; hết hạn là cách để một lần nạp lại có hiệu lực
        /// mà không phải khởi động lại tiến trình.
        /// </summary>
        [Range(1, 1440)]
        public int CatalogCacheMinutes { get; set; } = 30;

        /// <summary>
        /// {0} = tên NPC, {1} = danh mục thực thể, {2} = danh mục loại quan hệ, {3} = số thực thể tối
        /// đa, {4} = số loại quan hệ tối đa, {5} = tên trường thực thể, {6} = tên trường quan hệ.
        /// Ngoặc nhọn literal (ví dụ JSON mẫu) PHẢI viết <c>{{ }}</c>.
        /// </summary>
        public string SystemPromptTemplate { get; set; } = string.Empty;

        /// <summary>{0} = câu hỏi đã chuẩn hóa.</summary>
        public string UserPromptTemplate { get; set; } = string.Empty;

        /// <summary>Một dòng của danh mục thực thể: {0} = tên, {1} = các nhãn, {2} = phần bí danh (có thể rỗng).</summary>
        public string EntityLineTemplate { get; set; } = "- {0} ({1}{2})";

        /// <summary>Phần bí danh gắn vào dòng thực thể: {0} = các bí danh.</summary>
        public string EntityAliasTemplate { get; set; } = "; bí danh: {0}";

        public string LabelSeparator { get; set; } = ", ";

        public string AliasSeparator { get; set; } = ", ";

        /// <summary>Một dòng của danh mục quan hệ: {0} = tên loại, {1} = nhãn hiển thị.</summary>
        public string RelationLineTemplate { get; set; } = "- {0}: {1}";

        /// <summary>Ngăn giữa các dòng trong cả hai danh mục.</summary>
        public string CatalogSeparator { get; set; } = "\n";

        public string BuildSystemPrompt(string npcName, string entityCatalog, string relationCatalog) =>
            string.Format(SystemPromptTemplate,
                          npcName,
                          entityCatalog,
                          relationCatalog,
                          MaxEntities,
                          MaxRelationTypes,
                          GraphExtractionFields.Entities,
                          GraphExtractionFields.RelationTypes);

        public string BuildUserPrompt(string question) => string.Format(UserPromptTemplate, question);

        public string BuildEntityLine(string name, IReadOnlyList<string> labels, IReadOnlyList<string> aliases) =>
            string.Format(EntityLineTemplate,
                          name,
                          string.Join(LabelSeparator, labels),
                          aliases.Count == 0
                              ? string.Empty
                              : string.Format(EntityAliasTemplate, string.Join(AliasSeparator, aliases)));

        public string BuildRelationLine(string relation, string displayName) =>
            string.Format(RelationLineTemplate, relation, displayName);

        /// <summary>
        /// Chạy thử mọi template ngay lúc khởi động. Một dấu <c>{</c> quên nhân đôi trong JSON mẫu
        /// của prompt KHÔNG làm app chết — nó làm MỌI request ném <c>FormatException</c>, bị bắt và
        /// biến thành "suy biến" vĩnh viễn: đồ thị lặng lẽ không bao giờ đóng góp gì và không câu trả
        /// lời nào được ghi cache. Chặn khởi động ở đây rẻ hơn nhiều so với lần ra điều đó qua số liệu.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(SystemPromptTemplate))
            {
                yield return new ValidationResult(
                    $"{SectionName}:SystemPromptTemplate không được để trống.",
                    new[] { nameof(SystemPromptTemplate) });
            }
            else if (!SystemPromptTemplate.Contains("{1}", StringComparison.Ordinal) ||
                     !SystemPromptTemplate.Contains("{2}", StringComparison.Ordinal))
            {
                yield return new ValidationResult(
                    $"{SectionName}:SystemPromptTemplate phải chứa {{1}} (danh mục thực thể) và {{2}} " +
                    "(danh mục quan hệ); thiếu một cái thì LLM không có gì để chọn.",
                    new[] { nameof(SystemPromptTemplate) });
            }

            if (string.IsNullOrWhiteSpace(UserPromptTemplate) ||
                !UserPromptTemplate.Contains("{0}", StringComparison.Ordinal))
            {
                yield return new ValidationResult(
                    $"{SectionName}:UserPromptTemplate phải chứa {{0}} (câu hỏi).",
                    new[] { nameof(UserPromptTemplate) });
            }

            var broken = new List<string>();

            if (!TryFormat(() => BuildSystemPrompt(string.Empty, string.Empty, string.Empty)))
                broken.Add(nameof(SystemPromptTemplate));

            if (!TryFormat(() => BuildUserPrompt(string.Empty)))
                broken.Add(nameof(UserPromptTemplate));

            if (!TryFormat(() => BuildEntityLine(string.Empty, Array.Empty<string>(), new[] { string.Empty })))
                broken.Add($"{nameof(EntityLineTemplate)}/{nameof(EntityAliasTemplate)}");

            if (!TryFormat(() => BuildRelationLine(string.Empty, string.Empty)))
                broken.Add(nameof(RelationLineTemplate));

            if (broken.Count > 0)
            {
                yield return new ValidationResult(
                    $"{SectionName}: template sai cú pháp string.Format ({string.Join(", ", broken)}). " +
                    "Ngoặc nhọn literal phải viết {{ và }}, placeholder phải nằm trong số được mô tả.",
                    broken);
            }
        }

        private static bool TryFormat(Func<string> format)
        {
            try
            {
                format();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
