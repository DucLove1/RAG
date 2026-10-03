using System.Text.Json.Serialization;

namespace RAG.Class.Constants
{
    /// <summary>
    /// Ai nói một lượt trong lịch sử hội thoại. Bind từ JSON dạng chuỗi (<c>"player"</c> /
    /// <c>"npc"</c>, không phân biệt hoa thường) thay vì số, để client không phải nhớ giá trị enum.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ConversationRole>))]
    public enum ConversationRole
    {
        Player = 0,
        Npc = 1
    }
}
