using System.Text.Json;
using Microsoft.Extensions.Options;
using RAG.Class.Dto;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Extension;
using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Trích thực thể và ý định quan hệ bằng LLM, trong phạm vi danh mục NPC được biết.
    /// <para>
    /// Không tin LLM: mọi tên nó trả về đều đi qua <see cref="NpcEntityCatalog.Resolve"/> và mọi loại
    /// quan hệ đều phải có trong ontology. Tên lạ bị bỏ chứ không làm hỏng lượt trích — LLM tự đặt
    /// tên mới là chuyện sẽ xảy ra, và một cái tên bịa không được thành hạt giống.
    /// </para>
    /// <para>
    /// Nằm trên đường nóng, song song với truy hồi vector, nên có hạn chót riêng
    /// (<see cref="GraphExtractionConfig.TimeoutMs"/>). Hết hạn thì HỦY hẳn lượt gọi HTTP chứ không chỉ
    /// thôi chờ: một lượt gọi bị bỏ rơi vẫn đốt hạn mức, và nếu dính 429 thì còn đánh dấu nhầm khóa
    /// API trong pool dùng chung.
    /// </para>
    /// </summary>
    public sealed class LlmGraphEntityExtractor : IGraphEntityExtractor
    {
        private readonly ILLMProvider _llmProvider;
        private readonly LlmRequestOptions _requestOptions;
        private readonly IGraphEntityCatalog _catalog;
        private readonly GraphExtractionConfig _config;
        private readonly ILogger<LlmGraphEntityExtractor> _logger;

        /// <summary>Danh mục loại quan hệ đã render. Ontology không đổi trong vòng đời tiến trình nên dựng một lần.</summary>
        private readonly string _relationCatalog;

        /// <summary>Tên loại quan hệ LLM trả về (không phân biệt hoa thường) → tên chuẩn trong ontology.</summary>
        private readonly IReadOnlyDictionary<string, string> _relationNames;

        public LlmGraphEntityExtractor(ILlmProviderResolver llmProviderResolver,
                                       IGraphEntityCatalog catalog,
                                       IOntology ontology,
                                       IOptions<GraphExtractionConfig> options,
                                       ILogger<LlmGraphEntityExtractor> logger)
        {
            _config = options.Value;
            _llmProvider = llmProviderResolver.Resolve(_config.Provider);
            _requestOptions = new LlmRequestOptions(_config.Model, _config.ThinkingLevel);
            _catalog = catalog;
            _logger = logger;

            // Sắp theo tên: prompt phải giống hệt nhau giữa các lần khởi động, không phụ thuộc thứ tự
            // khai báo trong ontology.json hay thứ tự duyệt của từ điển.
            var relations = ontology.Relations.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();

            _relationNames = relations.ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);
            _relationCatalog = string.Join(
                _config.CatalogSeparator,
                relations.Select(name => _config.BuildRelationLine(name, ontology.DisplayRelation(name))));
        }

        public async Task<GraphExtraction> ExtractAsync(string npcName,
                                                        string question,
                                                        CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question) || question.Length > _config.MaxInputLength)
            {
                _logger.LogDebug("Bỏ qua trích thực thể: câu rỗng hoặc dài quá {Max} ký tự.", _config.MaxInputLength);
                return GraphExtraction.Empty;
            }

            var catalog = await _catalog.GetAsync(npcName, cancellationToken);

            if (catalog.Failed)
                return GraphExtraction.FailedResult;

            // NPC lạ: không có gì để chọn thì không tốn lượt gọi LLM nào.
            if (catalog.IsEmpty)
                return GraphExtraction.Empty;

            var systemPrompt = _config.BuildSystemPrompt(catalog.SelfName ?? npcName,
                                                         RenderEntities(catalog),
                                                         _relationCatalog);

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMilliseconds(_config.TimeoutMs));

            string output;

            try
            {
                output = await _llmProvider.AskAsync(systemPrompt,
                                                     _config.BuildUserPrompt(question),
                                                     _requestOptions,
                                                     deadline.Token);
            }
            // Thứ tự hai filter là thứ tự ưu tiên: caller hủy thật thì ném tiếp để cả pipeline dừng;
            // chỉ hạn chót của RIÊNG bước này mới được biến thành suy biến. Một catch OCE trơn (như ở
            // các node LLM khác) sẽ ném cả hạn chót này lên thành 500.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                _logger.LogWarning("Trích thực thể cho {Npc} quá {Timeout}ms, đồ thị suy biến ở lượt này.",
                    npcName, _config.TimeoutMs);
                return GraphExtraction.FailedResult;
            }
            catch (Exception exception)
            {
                // Gồm cả timeout riêng của HttpClient (cũng là OCE nhưng không đến từ hai token trên),
                // lỗi HTTP và hết khóa API.
                _logger.LogWarning(exception, "Trích thực thể cho {Npc} thất bại, đồ thị suy biến ở lượt này.", npcName);
                return GraphExtraction.FailedResult;
            }

            return Interpret(npcName, output, catalog);
        }

        private string RenderEntities(NpcEntityCatalog catalog) =>
            string.Join(
                _config.CatalogSeparator,
                catalog.Entries.Select(entry => _config.BuildEntityLine(entry.Name, entry.Labels, entry.Aliases)));

        /// <summary>
        /// Đọc JSON và hậu kiểm. JSON hỏng là suy biến; JSON hợp lệ nhưng không có thực thể nào là
        /// kết quả bình thường (câu hỏi không nói về thứ gì NPC biết).
        /// </summary>
        private GraphExtraction Interpret(string npcName, string output, NpcEntityCatalog catalog)
        {
            var json = LlmOutputText.ExtractJsonObject(output);

            if (json is null)
            {
                _logger.LogWarning("Đầu ra trích thực thể cho {Npc} không chứa đối tượng JSON: \"{Output}\".", npcName, output);
                return GraphExtraction.FailedResult;
            }

            List<string> mentions;
            List<string> intents;

            try
            {
                using var document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Đầu ra trích thực thể cho {Npc} không phải đối tượng JSON: \"{Output}\".", npcName, output);
                    return GraphExtraction.FailedResult;
                }

                mentions = ReadStrings(document.RootElement, GraphExtractionFields.Entities);
                intents = ReadStrings(document.RootElement, GraphExtractionFields.RelationTypes);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Đầu ra trích thực thể cho {Npc} là JSON hỏng: \"{Output}\".", npcName, output);
                return GraphExtraction.FailedResult;
            }

            // Khử trùng SAU khi đối chiếu (hai cách gọi khác nhau có thể về cùng một tên chuẩn) và cắt
            // SAU khi khử trùng, để tên bịa hay tên lặp không chiếm chỗ của tên thật. Giữ nguyên thứ
            // tự LLM trả về: đó là thứ hạng hạt giống.
            var entities = mentions
                .Select(catalog.Resolve)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .Take(_config.MaxEntities)
                .ToList();

            var relationTypes = intents
                .Select(intent => _relationNames.TryGetValue(intent.Trim(), out var name) ? name : null)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .Take(_config.MaxRelationTypes)
                .ToList();

            _logger.LogDebug(
                "Trích thực thể {Npc}: {Entities} | quan hệ: {Relations} (đầu ra thô: \"{Output}\").",
                npcName, string.Join(", ", entities), string.Join(", ", relationTypes), output);

            return new GraphExtraction(entities, relationTypes, Failed: false);
        }

        /// <summary>Trường thiếu hoặc không phải mảng thì coi như rỗng; phần tử không phải chuỗi thì bỏ.</summary>
        private static List<string> ReadStrings(JsonElement root, string field)
        {
            if (!root.TryGetProperty(field, out var array) || array.ValueKind != JsonValueKind.Array)
                return new List<string>();

            return array.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .OfType<string>()
                .ToList();
        }
    }
}
