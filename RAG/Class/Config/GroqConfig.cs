using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    public class GroqConfig
    {
        public const string SectionName = "GROQ";
        [Required(AllowEmptyStrings = false)]
        public string ApiKey { get; set; } = string.Empty;
        [Required(AllowEmptyStrings = false)]
        public string Model { get; set; } = string.Empty;

        /// <summary>Endpoint tương thích OpenAI của Groq.</summary>
        [Required(AllowEmptyStrings = false)]
        [Url]
        public string Url { get; set; } = "https://api.groq.com/openai/v1";
    }
}
