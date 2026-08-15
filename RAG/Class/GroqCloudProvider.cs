using OpenAI.Chat;
using RAG.Interface;

namespace RAG.Class
{
    public class GroqCloudProvider : ILLMProvider
    {
        private readonly ChatClient _chatClient;

        public GroqCloudProvider(ChatClient chatClient)
        {
            _chatClient = chatClient;
        }
        public async Task<string> AskAsync(string system, string user, CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(system),
                new UserChatMessage(user)
            };

            var response = await _chatClient.CompleteChatAsync(
                messages: messages,
                cancellationToken: cancellationToken);
            
            return response.Value.Content.Count > 0 ?
                response.Value.Content[0].Text.ToString():
                string.Empty;
        }
    }
}
