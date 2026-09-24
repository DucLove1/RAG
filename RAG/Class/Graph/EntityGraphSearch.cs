using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Local search gieo hạt bằng thực thể: LLM chọn trong danh mục NPC được biết những thực thể mà
    /// câu hỏi nhắc tới, rồi mở rộng một bậc quanh chúng.
    /// <para>
    /// Là bản tương đương local search của Microsoft GraphRAG, với một khác biệt nền tảng: đồ thị ở
    /// đây biểu diễn TRI THỨC CỦA TỪNG NPC chứ không phải tri thức của hệ thống. Vì vậy mọi bước
    /// dưới đây đều mang theo tên NPC, và cùng một câu hỏi gửi tới hai NPC khác nhau cho ra hai tập
    /// cạnh khác nhau — đó là tính năng, không phải giới hạn.
    /// </para>
    /// <para>
    /// Hạt giống KHÔNG lấy từ nhánh vector: hai nhánh chạy song song, và khi truy hồi vector trượt
    /// dòng đúng thì đồ thị không bị trượt theo. Bốn bước: trích thực thể → mở rộng một bậc → khử
    /// trùng → gom mã dòng để nhánh RAG tra nguyên văn. Lọc quyền, khử trùng cạnh đối xứng và sắp
    /// xếp nằm trong MỘT câu Cypher (<see cref="GraphCypher.ExpandTemplate"/>) chứ không rải ra đây,
    /// vì làm ở đây nghĩa là kéo về ứng dụng những cạnh mà NPC không được phép nhìn thấy rồi mới bỏ đi.
    /// </para>
    /// </summary>
    public sealed class EntityGraphSearch : IGraphSearch
    {
        private readonly IGraphEntityExtractor _extractor;
        private readonly IGraphStore _store;
        private readonly GraphSearchConfig _config;

        public EntityGraphSearch(IGraphEntityExtractor extractor,
                                 IGraphStore store,
                                 IOptions<GraphSearchConfig> config)
        {
            _extractor = extractor;
            _store = store;
            _config = config.Value;
        }

        public async Task<GraphContext> SearchAsync(GraphSearchQuery query,
                                                    CancellationToken cancellationToken = default)
        {
            var extraction = await _extractor.ExtractAsync(query.NpcName, query.Question, cancellationToken);

            // Suy biến phải đi lên tới tầng cache: "không trích được" và "câu hỏi không nhắc thực thể
            // nào" cùng cho ra 0 cạnh, nhưng câu trả lời của trường hợp đầu không được phép vào cache.
            if (extraction.Failed)
                return GraphContext.DegradedEmpty with { Extraction = extraction };

            // Không có hạt giống thì KHÔNG gọi Neo4j.
            if (extraction.Entities.Count == 0)
                return GraphContext.Empty with { Extraction = extraction };

            var budget = query.RelationBudget > 0 ? query.RelationBudget : _config.MaxRelations;

            var expansion = await _store.ExpandAsync(
                new GraphExpansionRequest(query.NpcName,
                                          extraction.Entities,
                                          extraction.RelationTypes,
                                          _config.ReasoningRelationsOnly,
                                          budget),
                cancellationToken);

            if (expansion.Failed)
                return GraphContext.DegradedEmpty with { Extraction = extraction };

            var edges = expansion.Items;

            if (edges.Count == 0)
                return GraphContext.Empty with { Extraction = extraction };

            var relations = new List<GraphRelation>(edges.Count);
            var supporting = new List<string>();

            // Khử trùng theo ElementId ở tầng này NGOÀI bước gộp WITH r trong Cypher. Không thừa:
            // đây là lưới an toàn cho đúng bất biến mà prompt phụ thuộc vào — một sự thật xuất hiện
            // hai lần trong danh sách quan hệ sẽ được mô hình đọc như hai bằng chứng độc lập.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var codes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var edge in edges)
            {
                if (!seen.Add(edge.ElementId))
                    continue;

                relations.Add(new GraphRelation(edge.Source, edge.Relation, edge.Target,
                                                edge.Status, edge.Negated, edge.ChunkCodes));

                foreach (var code in edge.ChunkCodes)
                {
                    if (codes.Add(code))
                        supporting.Add(code);

                    // Dòng liền kề tính bằng SỐ HỌC ngay ở đây, không hỏi đồ thị. Đồ thị không chứa
                    // văn bản nên nó không có gì để trả lời về chuyện này; mã sinh ra có thể không
                    // tồn tại và bước tra văn bản chỉ đơn giản không tìm thấy.
                    foreach (var neighbour in ChunkCodes.Neighbours(code, _config.AdjacentLineRadius))
                    {
                        if (codes.Add(neighbour))
                            supporting.Add(neighbour);
                    }
                }
            }

            return new GraphContext(relations, supporting, Degraded: false, extraction);
        }
    }
}
