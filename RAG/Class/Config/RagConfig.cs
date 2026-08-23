namespace RAG.Class.Config
{
    public class RagConfig
    {
        public const string SectionName = "RAG";

        public int ChunkSize { get; set; } = 500;
        public int ChunkOverlap { get; set; } = 50;
        public int TopK { get; set; } = 5;
    }
}
