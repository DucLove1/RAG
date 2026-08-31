using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    public class RagConfig
    {
        public const string SectionName = "RAG";

        [Range(1, int.MaxValue)]
        public int ChunkSize { get; set; } = 500;
        [Range(0, int.MaxValue)]
        public int ChunkOverlap { get; set; } = 50;
        [Range(1, 100)]
        public int TopK { get; set; } = 5;
    }
}
