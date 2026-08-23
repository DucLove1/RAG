using RAG.Class.Constants;

namespace RAG.Class.Config
{
    /// <summary>
    /// Chọn provider mặc định dùng để sinh câu trả lời cuối cùng của pipeline.
    /// </summary>
    public class LlmSelectionConfig
    {
        public const string SectionName = "LLM";

        public LlmProviderKey Provider { get; set; } = LlmProviderKey.Groq;
    }
}
