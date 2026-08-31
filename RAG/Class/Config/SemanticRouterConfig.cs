using RAG.Class.Constants;
using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình node định tuyến ngữ nghĩa. Route chỉ là bộ lọc "trả lời thẳng":
    /// KHÔNG khai báo route cho RAG, vì RAG là đường mặc định khi không route nào khớp.
    /// <para>
    /// Bảng <see cref="Routes"/> dùng CHUNG cho cả hai chiến lược — tên, template prompt và câu
    /// mẫu là mô tả của chính route, không phụ thuộc vào cách nhận diện nó. Núm chỉnh riêng của
    /// từng chiến lược nằm trong <see cref="Embedding"/> và <see cref="Llm"/>, để một khóa như
    /// <c>MaxRoutableLength</c> không còn đọc như luật chung của cả node trong khi thực ra chỉ
    /// chiến lược embedding tuân theo nó.
    /// </para>
    /// <para>
    /// Cố tình bind qua <c>IOptions</c> chứ không phải <c>IOptionsMonitor</c>. Ràng buộc này giờ
    /// được ba chỗ dựa vào: router embedding giữ vector dẫn xuất từ <see cref="Routes"/> nên
    /// reload-on-change sẽ làm cấu hình lệch khỏi cache; router LLM dựng sẵn prompt và từ điển
    /// tên route ngay trong constructor; và cache quyết định định tuyến giữ THAM CHIẾU tới
    /// <c>RouteMatch</c>. Đổi bảng route đòi hỏi khởi động lại ứng dụng.
    /// </para>
    /// </summary>
    public class SemanticRouterConfig : IValidatableObject
    {
        public const string SectionName = "SemanticRouter";

        /// <summary>Chiến lược định tuyến đang chạy; <c>Off</c> thì pipeline dùng bản passthrough.</summary>
        public SemanticRouterStrategy Strategy { get; set; } = SemanticRouterStrategy.Llm;

        /// <summary>Bảng route dùng chung cho mọi chiến lược.</summary>
        public List<SemanticRouteConfig> Routes { get; set; } = new();

        /// <summary>Núm chỉnh riêng của chiến lược <see cref="SemanticRouterStrategy.Embedding"/>.</summary>
        public EmbeddingRouterConfig Embedding { get; set; } = new();

        /// <summary>Núm chỉnh riêng của chiến lược <see cref="SemanticRouterStrategy.Llm"/>.</summary>
        public LlmRouterConfig Llm { get; set; } = new();

        /// <summary>
        /// Chỉ chặn những thứ KHÔNG thể thoái hóa an toàn. Route thiếu template hay thiếu câu mẫu
        /// vẫn chỉ bị bỏ qua kèm log cảnh báo như trước — hệ thống mất một route nhưng vẫn trả lời
        /// được, nên chặn khởi động vì lý do đó là đánh đổi tồi.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var names = Routes
                .Select(route => route.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            // Trùng tên là lỗi câm: từ điển tên -> route của router LLM sẽ âm thầm nuốt mất một
            // route, và router embedding thì chấm điểm cả hai rồi lấy cái điểm cao hơn. Hai hành
            // vi khác nhau cho cùng một cấu hình sai.
            var duplicates = names
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                yield return new ValidationResult(
                    $"Tên route bị trùng (không phân biệt hoa thường): {string.Join(", ", duplicates)}.",
                    new[] { nameof(Routes) });
            }

            if (Strategy != SemanticRouterStrategy.Llm)
                yield break;

            if (string.IsNullOrWhiteSpace(Llm.NoMatchLabel))
            {
                yield return new ValidationResult(
                    "Llm:NoMatchLabel không được để trống: không có nhãn này thì LLM không có cách nào " +
                    "nói \"không route nào khớp\", và mọi câu hỏi tri thức sẽ bị ép vào một route.",
                    new[] { $"{nameof(Llm)}.{nameof(LlmRouterConfig.NoMatchLabel)}" });
            }
            else if (names.Any(name => string.Equals(name, Llm.NoMatchLabel, StringComparison.OrdinalIgnoreCase)))
            {
                yield return new ValidationResult(
                    $"Llm:NoMatchLabel (\"{Llm.NoMatchLabel}\") trùng tên một route, nên không phân biệt " +
                    "được \"khớp route đó\" với \"không khớp gì cả\".",
                    new[] { $"{nameof(Llm)}.{nameof(LlmRouterConfig.NoMatchLabel)}" });
            }

            if (string.IsNullOrWhiteSpace(Llm.SystemPromptTemplate))
            {
                yield return new ValidationResult(
                    "Llm:SystemPromptTemplate không được để trống khi Strategy = Llm.",
                    new[] { $"{nameof(Llm)}.{nameof(LlmRouterConfig.SystemPromptTemplate)}" });
            }
            else if (!Llm.SystemPromptTemplate.Contains("{0}", StringComparison.Ordinal) ||
                     !Llm.SystemPromptTemplate.Contains("{1}", StringComparison.Ordinal))
            {
                yield return new ValidationResult(
                    "Llm:SystemPromptTemplate phải chứa cả {0} (danh mục route) lẫn {1} (nhãn không khớp); " +
                    "thiếu một trong hai thì LLM không nhìn thấy tập nhãn được phép chọn.",
                    new[] { $"{nameof(Llm)}.{nameof(LlmRouterConfig.SystemPromptTemplate)}" });
            }

            if (string.IsNullOrWhiteSpace(Llm.UserPromptTemplate) ||
                !Llm.UserPromptTemplate.Contains("{0}", StringComparison.Ordinal))
            {
                yield return new ValidationResult(
                    "Llm:UserPromptTemplate phải chứa {0} (câu của người chơi).",
                    new[] { $"{nameof(Llm)}.{nameof(LlmRouterConfig.UserPromptTemplate)}" });
            }
        }
    }

    /// <summary>
    /// Một route "trả lời thẳng": khớp thì bỏ qua truy hồi Qdrant và để LLM sinh câu trả lời
    /// theo persona NPC nhưng không kèm ngữ cảnh nào.
    /// </summary>
    public class SemanticRouteConfig
    {
        /// <summary>
        /// Tên route. Với chiến lược embedding đây chỉ là nhãn cho log; với chiến lược LLM nó là
        /// TOKEN mà mô hình phải xuất ra, nên nên giữ ngắn, không dấu và không khoảng trắng.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Mô tả ý định của route bằng tiếng Việt tự nhiên, dành cho LLM đọc. Chiến lược embedding
        /// không dùng tới. Đây là chỗ đắt giá nhất để tinh chỉnh độ chính xác của router LLM:
        /// một câu mô tả rõ ranh giới hiệu quả hơn thêm mười câu mẫu.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Các câu mẫu đại diện cho route. Phục vụ hai vai trò: chiến lược embedding nhúng chúng
        /// thành vector, chiến lược LLM lấy vài câu đầu làm ví dụ few-shot trong prompt.
        /// <para>
        /// Vì chiến lược LLM lấy <c>Llm:MaxExamplesPerRoute</c> câu ĐẦU tiên, THỨ TỰ trong danh
        /// sách này có ý nghĩa: hãy đặt câu tiêu biểu nhất lên trước, và để các biến thể không dấu
        /// hay viết tắt xuống cuối (bước chuẩn hóa đã phục hồi dấu trước khi định tuyến).
        /// </para>
        /// </summary>
        public List<string> Utterances { get; set; } = new();

        /// <summary>
        /// Ngưỡng cosine riêng của route; bỏ trống thì lấy <c>Embedding:SimilarityThreshold</c>.
        /// Chỉ chiến lược embedding đọc tới.
        /// </summary>
        public double? SimilarityThreshold { get; set; }

        /// <summary>
        /// System prompt riêng cho câu trả lời không có ngữ cảnh; {0} = tên NPC, {1} = tính cách NPC.
        /// Bỏ trống thì dùng Prompts:AnswerSystemTemplate — nhưng lưu ý template mặc định có câu
        /// "nếu không có ngữ cảnh thì trả lời không biết", vốn phản tác dụng trên nhánh này.
        /// Dấu ngoặc nhọn literal phải escape thành {{ }} vì template đi qua string.Format.
        /// </summary>
        public string SystemPromptTemplate { get; set; } = string.Empty;

        /// <summary>Template user prompt cho câu trả lời không truy hồi; {0} = câu hỏi đã chuẩn hóa.</summary>
        public string UserPromptTemplate { get; set; } = string.Empty;
    }

    /// <summary>
    /// Núm chỉnh của chiến lược định tuyến bằng cosine similarity.
    /// Chiến lược LLM KHÔNG đọc bất cứ giá trị nào ở đây.
    /// </summary>
    public class EmbeddingRouterConfig
    {
        /// <summary>Ngưỡng cosine mặc định; route nào không tự khai ngưỡng thì dùng giá trị này.</summary>
        [Range(0d, 1d)]
        public double SimilarityThreshold { get; set; } = 0.80;

        /// <summary>
        /// Câu dài hơn ngưỡng này thì bỏ qua việc chấm điểm và đi thẳng đường RAG.
        /// Đây là phòng thủ rẻ nhất cho câu pha trộn ý định ("chào bạn, cho tôi hỏi giá kiếm sắt"):
        /// cosine không phân biệt được loại câu đó, mà câu tán gẫu thật gần như luôn ngắn.
        /// Chiến lược LLM không cần cửa chặn này vì nó đọc hiểu được câu pha trộn.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int MaxRoutableLength { get; set; } = 60;

        /// <summary>
        /// Nơi lưu vector câu mẫu để lần khởi động sau không phải gọi lại API embedding.
        /// Để trống thì tắt cache (luôn nhúng lại mỗi lần khởi động).
        /// </summary>
        public string VectorCachePath { get; set; } = "App_Data/route-vectors.json";

        /// <summary>
        /// Nơi lưu câu mẫu được thêm lúc chạy qua endpoint, tách khỏi appsettings.json.
        /// Để trống thì câu mẫu thêm vào chỉ sống trong bộ nhớ tới lần khởi động lại.
        /// </summary>
        public string UtteranceStorePath { get; set; } = "App_Data/route-utterances.json";

        /// <summary>Khoảng nghỉ giữa hai lần thử nạp vector khi warm-up thất bại.</summary>
        [Range(1, int.MaxValue)]
        public int WarmupRetryDelaySeconds { get; set; } = 30;

        /// <summary>Số lần thử nạp vector tối đa trước khi từ bỏ tới lần khởi động sau.</summary>
        [Range(1, int.MaxValue)]
        public int WarmupMaxAttempts { get; set; } = 5;
    }

    /// <summary>
    /// Núm chỉnh của chiến lược định tuyến bằng LLM: một lượt gọi mô hình đọc câu của người chơi
    /// rồi xuất ra đúng một nhãn route.
    /// <para>
    /// Toàn bộ prompt nằm ở configuration để tinh chỉnh độ chính xác không cần build lại — đúng
    /// cách <see cref="QueryNormalizationConfig"/> đã làm với node chuẩn hóa.
    /// </para>
    /// </summary>
    public class LlmRouterConfig
    {
        /// <summary>
        /// Provider dùng để phân loại, độc lập với provider trả lời. Mặc định Gemini để việc phân
        /// loại không tiêu hạn mức của pool key đang gánh đường trả lời.
        /// </summary>
        public LlmProviderKey Provider { get; set; } = LlmProviderKey.Gemini;

        /// <summary>
        /// Model riêng cho bước phân loại. Để trống thì dùng model mặc định của provider
        /// (<c>GEMINILLM:Model</c> hoặc <c>GROQ:Model</c> trong appsettings.json). Đây là núm để phân loại chạy model
        /// khác với node chuẩn hóa và đường trả lời, trong khi vẫn chung pool API key.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Số câu mẫu tối đa lấy từ mỗi route làm ví dụ few-shot. Lấy các câu ĐẦU danh sách,
        /// nên thứ tự trong <see cref="SemanticRouteConfig.Utterances"/> có ý nghĩa.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int MaxExamplesPerRoute { get; set; } = 8;

        /// <summary>
        /// Câu dài hơn ngưỡng này đi thẳng đường RAG mà KHÔNG gọi LLM phân loại. Đây không phải
        /// bản sao của <see cref="EmbeddingRouterConfig.MaxRoutableLength"/> — nó nới rộng hơn
        /// nhiều và chỉ nhằm chặn đoạn văn dài, còn ranh giới câu pha trộn ý định thì để luật
        /// trong prompt xử lý.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int MaxInputLength { get; set; } = 200;

        /// <summary>
        /// Nhãn mà LLM phải xuất ra khi không route nào khớp. Phải khác mọi tên route.
        /// Nhận được nhãn này nghĩa là mô hình đã QUYẾT ĐỊNH đi đường RAG, khác với việc
        /// không phân tích được đầu ra (cũng đi RAG, nhưng là fail-open).
        /// </summary>
        public string NoMatchLabel { get; set; } = "khong_khop";

        /// <summary>System prompt phân loại; {0} = danh mục route đã dựng, {1} = nhãn không khớp.</summary>
        public string SystemPromptTemplate { get; set; } = string.Empty;

        /// <summary>Template user prompt; {0} = câu của người chơi đã chuẩn hóa.</summary>
        public string UserPromptTemplate { get; set; } = string.Empty;

        /// <summary>Một dòng trong danh mục route; {0} = tên, {1} = mô tả, {2} = các ví dụ đã nối.</summary>
        public string RouteBlockTemplate { get; set; } = "- {0}: {1}\n  Ví dụ: {2}";

        /// <summary>Ký tự nối giữa các dòng route trong danh mục.</summary>
        public string RouteBlockSeparator { get; set; } = "\n";

        /// <summary>Ký tự nối giữa các ví dụ few-shot của cùng một route.</summary>
        public string ExampleSeparator { get; set; } = " | ";

        public string BuildSystemPrompt(string routeCatalog) =>
            string.Format(SystemPromptTemplate, routeCatalog, NoMatchLabel);

        public string BuildUserPrompt(string question) =>
            string.Format(UserPromptTemplate, question);
    }
}
