using System.Text.Json.Serialization;

namespace RAG.Class.Constants
{
    /// <summary>
    /// Mức suy nghĩ (thinkingLevel) của dòng Gemini 3.x. Tên member bind trực tiếp từ configuration,
    /// còn <see cref="JsonStringEnumMemberNameAttribute"/> giữ đúng giá trị mà API nhận trên dây.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GeminiThinkingLevel>))]
    public enum GeminiThinkingLevel
    {
        [JsonStringEnumMemberName("minimal")]
        Minimal = 0,

        [JsonStringEnumMemberName("low")]
        Low = 1,

        [JsonStringEnumMemberName("medium")]
        Medium = 2,

        [JsonStringEnumMemberName("high")]
        High = 3
    }
}
