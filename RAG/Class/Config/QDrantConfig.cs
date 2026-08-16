namespace RAG.Class.Config
{
    public class QDrantConfig
    {
        public const string SectionName = "QDRANT";
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 32772;
        public string Collection { get; set; } = "basic-collection";
        public string ApiKey
        {
            get; set;
        } = string.Empty;
        public int Dimensions { get; set; } = 768;
    }
}
