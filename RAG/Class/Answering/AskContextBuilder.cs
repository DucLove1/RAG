using System.Text;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Answering
{
    /// <summary>
    /// Dựng ngữ cảnh truy hồi từ HAI NHÁNH CHẠY CONCURRENCY, gộp ở cuối:
    /// <para>
    /// [A] Qdrant KNN bằng vector câu hỏi đã có sẵn.
    /// [B] Đồ thị: LLM trích thực thể trong danh mục NPC được biết → Neo4j mở rộng quanh chúng → tra
    /// nguyên văn các dòng mà cạnh trỏ tới.
    /// </para>
    /// <para>
    /// Hai nhánh không phụ thuộc nhau: đồ thị không lấy hạt giống từ kết quả vector, nên khi vector
    /// trượt dòng đúng thì đồ thị không trượt theo. Thời gian dựng ngữ cảnh vì vậy xấp xỉ nhánh CHẬM
    /// HƠN chứ không phải tổng hai nhánh.
    /// </para>
    /// <para>
    /// Hai lời hứa mà lớp này giữ:
    /// </para>
    /// <para>
    /// 1. KHÔNG ĐỔI PROMPT CŨ. Các đoạn khớp vector luôn vào nguyên vẹn, nối y như bản trước, và
    /// ngân sách chỉ áp lên phần BỔ SUNG mà đồ thị mang tới. Đồ thị tắt hoặc không ra cạnh nào thì
    /// khối văn bản giống bản trước từng byte, nên mọi bài trong RAG.http và mọi entry cache cũ vẫn
    /// còn nghĩa.
    /// </para>
    /// <para>
    /// 2. CHIA NGÂN SÁCH CỐ ĐỊNH (<c>Graph:Search:GraphShareRatio</c>). Không chia thì một vùng lân
    /// cận dày đặc đẩy hết nguyên văn ra khỏi prompt, và mô hình còn lại một danh sách quan hệ mà nó
    /// không trích dẫn được. Đo bằng KÝ TỰ vì dự án không có tokenizer.
    /// </para>
    /// </summary>
    public sealed class AskContextBuilder : IAskContextBuilder
    {
        private static readonly IReadOnlyDictionary<string, ChunkText> NoTexts =
            new Dictionary<string, ChunkText>(StringComparer.Ordinal);

        private readonly IVectorStore _vectorStore;
        private readonly IGraphSearch _graphSearch;
        private readonly IChunkTextLookup _chunkTextLookup;
        private readonly IGraphContextRenderer _renderer;
        private readonly PromptConfig _prompts;
        private readonly GraphSearchConfig _budget;
        private readonly ILogger<AskContextBuilder> _logger;

        public AskContextBuilder(IVectorStore vectorStore,
                                 IGraphSearch graphSearch,
                                 IChunkTextLookup chunkTextLookup,
                                 IGraphContextRenderer renderer,
                                 IOptions<PromptConfig> prompts,
                                 IOptions<GraphSearchConfig> budget,
                                 ILogger<AskContextBuilder> logger)
        {
            _vectorStore = vectorStore;
            _graphSearch = graphSearch;
            _chunkTextLookup = chunkTextLookup;
            _renderer = renderer;
            _prompts = prompts.Value;
            _budget = budget.Value;
            _logger = logger;
        }

        public async Task<AskContext> BuildAsync(string npcName,
                                                 string question,
                                                 float[] questionEmbedding,
                                                 int topK,
                                                 CancellationToken cancellationToken = default)
        {
            // Không gọi EnsureCollectionExistsAsync ở đây: đường trả lời chỉ ĐỌC, và collection đã
            // được đảm bảo ở đường nạp dữ liệu.
            var filter = VectorSearchFilter.Match(PayloadFields.NpcNames, npcName);

            // Token riêng cho nhánh đồ thị để hủy được nó khi nhánh vector hỏng.
            using var graphCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // ===== KHỞI ĐỘNG CẢ HAI NHÁNH — CHƯA await CÁI NÀO =====
            // Gọi một hàm async mà không await thì hàm đó chạy ngay tới lượt I/O đầu tiên (gửi request
            // đi) rồi trả về một Task đang chạy dở. Hai dòng dưới vì vậy gửi request tới Neo4j/Gemini
            // và tới Qdrant gần như cùng lúc; từ đây hai nhánh cùng chờ mạng song song. Không cần
            // Task.Run: đây là việc chờ I/O, không phải việc nặng CPU. Nhánh đồ thị khởi động trước vì
            // nó là nhánh chậm (một lượt LLM, rồi Neo4j, rồi Qdrant).
            var graphTask = SearchGraphBranchAsync(npcName, question, graphCancellation.Token);
            var vectorTask = _vectorStore.SearchAsync(questionEmbedding, filter, topK, cancellationToken);

            // ===== NHẬN KẾT QUẢ =====
            // KHÔNG dùng Task.WhenAll: nó chỉ ném sau khi CẢ HAI task xong, nên Qdrant hỏng ở 50ms
            // vẫn bắt request chờ hết lượt LLM trích thực thể rồi mới báo lỗi. Nhận riêng nhánh vector
            // thì hủy được nhánh đồ thị ngay lúc đó.
            IReadOnlyList<VectorHit> hits;

            try
            {
                hits = await vectorTask;
            }
            catch
            {
                graphCancellation.Cancel();

                // Chờ nhánh đồ thị dừng hẳn (bỏ qua lỗi của nó) trước khi ném: không có việc gì của
                // request này được sống lâu hơn request, và token nguồn sắp bị dispose.
                await ((Task)graphTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                throw;
            }

            // Nhánh đồ thị đã chạy song song từ nãy; đây chỉ là chỗ nhận kết quả. Theo hợp đồng của
            // nó: chỉ ném khi chính caller hủy.
            var branch = await graphTask;
            var graph = branch.Graph;

            var graphBudget = (int)(_budget.ContextBudgetChars * _budget.GraphShareRatio);
            var (graphBlock, relationCount) = _renderer.Render(graph.Relations, graphBudget);

            var textBudget = _budget.ContextBudgetChars - graphBudget;
            if (_budget.AllowShareSpillover)
                textBudget += graphBudget - graphBlock.Length;

            var text = new StringBuilder(string.Join(
                _prompts.ContextSeparator,
                hits.Select(hit => hit.Payload[PayloadFields.Text])));

            // TryGetValue chứ không indexer: điểm nạp trước khi có mã chunk không mang trường này,
            // và chúng vẫn phải trả lời được — chỉ là không loại trùng được với nhánh đồ thị.
            var hitCodes = hits
                .Select(hit => hit.Payload.TryGetValue(PayloadFields.ChunkCode, out var code) ? code : null)
                .OfType<string>()
                .Where(code => code.Length > 0)
                .ToHashSet(StringComparer.Ordinal);

            var extraCount = AppendSupportingText(text, graph.SupportingChunkCodes, branch.Texts, hitCodes, textBudget);

            return new AskContext(text.ToString(), graphBlock, hits.Count, extraCount, relationCount, graph.Degraded);
        }

        /// <summary>
        /// Nhánh B: truy hồi đồ thị rồi tra luôn nguyên văn các dòng mà cạnh trỏ tới. Tra nguyên văn
        /// nằm TRONG nhánh này để nó cũng chạy song song với Qdrant, dù vì thế nó không biết trước
        /// dòng nào nhánh vector đã mang về — phần loại trùng làm ở bước gộp.
        /// <para>
        /// Không bao giờ ném, trừ khi caller hủy: nhánh đồ thị không được phép làm hỏng request. Tra
        /// nguyên văn lỗi thì vẫn giữ các quan hệ nhưng đánh dấu suy biến, để câu trả lời thiếu phần
        /// dẫn chứng không bị đóng băng vào cache.
        /// </para>
        /// </summary>
        private async Task<GraphBranch> SearchGraphBranchAsync(string npcName,
                                                               string question,
                                                               CancellationToken cancellationToken)
        {
            var graph = await _graphSearch.SearchAsync(
                new GraphSearchQuery(npcName, question, RelationBudget: 0), cancellationToken);

            if (graph.SupportingChunkCodes.Count == 0)
                return new GraphBranch(graph, NoTexts);

            try
            {
                // Tra qua IChunkTextLookup nghĩa là lọc quyền NPC lần nữa — mã do đồ thị chọn không
                // được tin mà không lọc lại.
                var texts = await _chunkTextLookup.GetByCodesAsync(npcName, graph.SupportingChunkCodes, cancellationToken);
                return new GraphBranch(graph, texts);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Tra nguyên văn cho {Count} mã chunk của đồ thị ({Npc}) thất bại; giữ quan hệ, bỏ phần dẫn chứng.",
                    graph.SupportingChunkCodes.Count, npcName);

                return new GraphBranch(graph with { Degraded = true }, NoTexts);
            }
        }

        /// <summary>
        /// Nối nguyên văn các dòng mà đồ thị trỏ tới, theo đúng thứ tự đồ thị trả về, bỏ các dòng
        /// nhánh vector đã mang về.
        /// <para>
        /// Mỗi dòng vào trọn vẹn hoặc không vào: cắt giữa câu cho mô hình một mệnh đề cụt mà nó sẽ
        /// tự điền nốt.
        /// </para>
        /// </summary>
        private int AppendSupportingText(StringBuilder text,
                                         IReadOnlyList<string> supportingCodes,
                                         IReadOnlyDictionary<string, ChunkText> found,
                                         IReadOnlySet<string> hitCodes,
                                         int textBudget)
        {
            var count = 0;

            foreach (var code in supportingCodes)
            {
                if (hitCodes.Contains(code) || !found.TryGetValue(code, out var chunk))
                    continue;

                var separator = text.Length > 0 ? _prompts.ContextSeparator : string.Empty;

                // Dừng hẳn chứ không nhảy cóc, cùng lý do với bộ render đồ thị: thứ tự đã là độ
                // liên quan, và một dòng ngắn kém liên quan không được chen vào chỗ dòng dài hơn.
                if (text.Length + separator.Length + chunk.Text.Length > textBudget)
                    break;

                text.Append(separator).Append(chunk.Text);
                count++;
            }

            return count;
        }

        private sealed record GraphBranch(GraphContext Graph, IReadOnlyDictionary<string, ChunkText> Texts);
    }
}
