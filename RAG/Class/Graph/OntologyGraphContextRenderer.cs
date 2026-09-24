using System.Text;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Render cạnh bằng nhãn hiển thị trong <c>ontology.json</c>.
    /// <para>
    /// Mỗi dòng mang nhãn <c>trang_thai</c> ("theo lời khai", "tin đồn"...). Đó là thứ duy nhất ngăn
    /// mô hình trình bày tin đồn như kết luận, nên nó không phải trang trí và không được bỏ khỏi
    /// template. Quan hệ luôn đi qua bản hai tham số của <see cref="IOntology.DisplayRelation(string, bool)"/>
    /// để cạnh phủ định không bị render thành lời khẳng định ngược nghĩa.
    /// </para>
    /// </summary>
    public sealed class OntologyGraphContextRenderer : IGraphContextRenderer
    {
        private readonly IOntology _ontology;
        private readonly GraphPromptConfig _config;

        public OntologyGraphContextRenderer(IOntology ontology, IOptions<PromptConfig> prompts)
        {
            _ontology = ontology;
            _config = prompts.Value.Graph;
        }

        public (string Block, int RelationCount) Render(IReadOnlyList<GraphRelation> relations, int budgetChars)
        {
            if (relations.Count == 0)
                return (string.Empty, 0);

            var builder = new StringBuilder(_config.BlockHeader);
            var count = 0;

            foreach (var relation in relations)
            {
                var line = string.Format(_config.RelationLineTemplate,
                                         relation.Source,
                                         _ontology.DisplayRelation(relation.Relation, relation.Negated),
                                         relation.Target,
                                         _ontology.DisplayStatus(relation.Status));

                // Dừng hẳn chứ không bỏ qua rồi thử cạnh sau: danh sách đã sắp theo độ liên quan,
                // nhảy cóc sẽ để một cạnh ngắn nhưng kém liên quan chen vào chỗ của cạnh dài hơn.
                if (builder.Length + _config.RelationSeparator.Length + line.Length > budgetChars)
                    break;

                builder.Append(_config.RelationSeparator).Append(line);
                count++;
            }

            return count == 0 ? (string.Empty, 0) : (builder.ToString(), count);
        }
    }
}
