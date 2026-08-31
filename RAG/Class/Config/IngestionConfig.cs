using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình đường nạp tri thức.
    /// </summary>
    public class IngestionConfig
    {
        public const string SectionName = "Ingestion";

        /// <summary>
        /// Các phần mở rộng được coi là văn bản thuần. Viết chữ thường, có dấu chấm đầu.
        /// </summary>
        [MinLength(1)]
        public List<string> PlainTextExtensions { get; set; } = new() { ".txt", ".md", ".json" };
    }
}
