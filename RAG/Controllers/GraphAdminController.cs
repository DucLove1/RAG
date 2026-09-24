using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Controllers
{
    /// <summary>
    /// Đường quản trị và chẩn đoán đồ thị tri thức.
    /// <para>
    /// Controller cố ý KHÔNG biết đồ thị đang bật hay tắt: khi tắt, composition root đăng ký
    /// <c>DisabledGraphAdmin</c> và mọi action ở đây tự thành 404. Nhờ vậy không có một dòng
    /// <c>if (enabled)</c> nào nhân bản ra năm chỗ.
    /// </para>
    /// </summary>
    [Route("api/graph")]
    [ApiController]
    public class GraphAdminController : ControllerBase
    {
        private readonly IGraphLoader _loader;
        private readonly IGraphSchemaAdmin _schemaAdmin;
        private readonly IGraphSearch _graphSearch;
        private readonly IGraphStatistics _statistics;
        private readonly IGraphEntityCatalog _catalog;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IAskContextBuilder _contextBuilder;
        private readonly PromptConfig _prompts;
        private readonly RagConfig _rag;

        public GraphAdminController(IGraphLoader loader,
                                    IGraphSchemaAdmin schemaAdmin,
                                    IGraphSearch graphSearch,
                                    IGraphStatistics statistics,
                                    IGraphEntityCatalog catalog,
                                    IEmbeddingProvider embeddingProvider,
                                    IAskContextBuilder contextBuilder,
                                    IOptions<PromptConfig> prompts,
                                    IOptions<RagConfig> rag)
        {
            _loader = loader;
            _schemaAdmin = schemaAdmin;
            _graphSearch = graphSearch;
            _statistics = statistics;
            _catalog = catalog;
            _embeddingProvider = embeddingProvider;
            _contextBuilder = contextBuilder;
            _prompts = prompts.Value;
            _rag = rag.Value;
        }

        /// <summary>Tạo constraint và index. Idempotent, và PHẢI chạy trước lần nạp đầu tiên.</summary>
        [HttpPost("indexes")]
        public async Task<IActionResult> PostIndexes(CancellationToken cancellationToken = default)
        {
            await _schemaAdmin.EnsureIndexesAsync(cancellationToken);
            return Ok();
        }

        [HttpPost("load")]
        public async Task<IActionResult> PostLoad(CancellationToken cancellationToken = default) =>
            Ok(await _loader.LoadAsync(cancellationToken));

        [HttpGet("verify")]
        public async Task<IActionResult> GetVerify(CancellationToken cancellationToken = default) =>
            Ok(await _loader.VerifyAsync(cancellationToken));

        /// <summary>
        /// Chạy truy hồi đồ thị rồi trả về nguyên kết quả, kèm hạt giống mà bước trích đã chọn
        /// (<c>extraction</c>). Tốn MỘT lượt LLM trích thực thể, không tốn lượt sinh câu trả lời nào.
        /// <para>
        /// Đây là công cụ quan trọng nhất của cả tầng đồ thị, tương đương <c>route-debug</c> ở node
        /// định tuyến: nó cho nhìn thẳng vào tập cạnh mà một NPC được phép thấy, nên bài kiểm phân
        /// quyền không phải đoán qua câu trả lời của mô hình.
        /// </para>
        /// <para>
        /// Endpoint này đi vòng qua cache câu trả lời, và Gemini chạy với temperature khác 0, nên hai
        /// lần gọi cùng một câu có thể chọn hạt giống khác nhau.
        /// </para>
        /// </summary>
        [HttpPost("local-search-debug")]
        public async Task<IActionResult> PostLocalSearchDebug([FromBody] GraphDebugRequest request,
                                                              CancellationToken cancellationToken = default)
        {
            var context = await _graphSearch.SearchAsync(
                new GraphSearchQuery(request.NpcName, request.Question, request.Limit ?? 0),
                cancellationToken);

            return Ok(context);
        }

        /// <summary>
        /// Dựng ĐÚNG ngữ cảnh và user prompt mà đường trả lời sẽ gửi cho LLM — rồi dừng trước bước
        /// sinh câu trả lời.
        /// <para>
        /// Tốn một lượt nhúng và, khi đồ thị bật, một lượt LLM trích thực thể. Câu hỏi đi thẳng vào,
        /// không qua bộ chuẩn hóa, bộ định tuyến hay cache: đây là chỗ nhìn phần truy hồi, không phải
        /// cả pipeline.
        /// </para>
        /// </summary>
        [HttpPost("context-debug")]
        public async Task<IActionResult> PostContextDebug([FromBody] ContextDebugRequest request,
                                                          CancellationToken cancellationToken = default)
        {
            var embedding = await _embeddingProvider.GetEmbeddingsAsync(request.Question, cancellationToken);

            var context = await _contextBuilder.BuildAsync(
                request.NpcName, request.Question, embedding, request.TopK ?? _rag.TopK, cancellationToken);

            return Ok(new
            {
                context.HitCount,
                context.ExtraTextCount,
                context.RelationCount,
                context.GraphDegraded,
                context.Cacheable,
                TextChars = context.Text.Length,
                GraphChars = context.Graph.Length,
                UserPrompt = _prompts.BuildUserPrompt(context.Text, context.Graph, request.Question)
            });
        }

        [HttpGet("stats")]
        public IActionResult GetStats() => Ok(_statistics.GetStats());

        /// <summary>
        /// Danh mục thực thể mà một NPC được biết — đúng "thực đơn" đưa cho LLM trích thực thể.
        /// <para>
        /// Dùng để kiểm phân quyền bằng mắt: danh mục của Jack không được có thực thể nào chỉ xuất
        /// thân từ hồ sơ pháp y. Không gọi LLM; đọc qua cache danh mục nên lần gọi thứ hai không chạm Neo4j.
        /// </para>
        /// </summary>
        [HttpGet("catalog-debug")]
        public async Task<IActionResult> GetCatalogDebug([FromQuery] string npcName,
                                                         CancellationToken cancellationToken = default)
        {
            var catalog = await _catalog.GetAsync(npcName, cancellationToken);

            return Ok(new
            {
                catalog.Failed,
                catalog.SelfName,
                Count = catalog.Entries.Count,
                catalog.AmbiguousKeys,
                catalog.Entries
            });
        }
    }

    public sealed record ContextDebugRequest(string NpcName, string Question, int? TopK);

    /// <param name="Question">
    /// Bắt buộc: hạt giống giờ là thực thể mà LLM trích từ câu hỏi, không còn đường nhập mã chunk.
    /// </param>
    /// <param name="Limit">Số cạnh tối đa; để trống thì dùng <c>Graph:Search:MaxRelations</c>.</param>
    public sealed record GraphDebugRequest(string NpcName,
                                           string Question,
                                           int? Limit);
}
