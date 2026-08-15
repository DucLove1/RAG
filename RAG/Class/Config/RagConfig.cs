namespace RAG.Class.Config
{
    public class RagConfig
    {
        public int chunkSize { get; set; } = 500;
        public int chunkOverlap { get; set; } = 50;
        public int topK { get; set; } = 5;
    }
}
