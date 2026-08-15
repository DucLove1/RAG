namespace RAG.Class.Config
{
    public class GroqConfig
    {
        public const string SectionName = "GROQ";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }
}
