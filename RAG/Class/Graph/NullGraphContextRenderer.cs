using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>Null Object khi đồ thị tắt: không có ontology, không có gì để render.</summary>
    public sealed class NullGraphContextRenderer : IGraphContextRenderer
    {
        public (string Block, int RelationCount) Render(IReadOnlyList<GraphRelation> relations, int budgetChars) =>
            (string.Empty, 0);
    }
}
