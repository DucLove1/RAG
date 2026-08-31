using OpenAI;
using OpenAI.Chat;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using System.Collections.Concurrent;
using System.ClientModel;
using Microsoft.Extensions.Options;

namespace RAG.Class
{
    public class GroqCloudProvider : ILLMProvider
    {
        private readonly IApiKeyRotator _rotator;
        private readonly GroqConfig _config;
        private readonly ConcurrentDictionary<string, ChatClient> _clientCache = new();
        private readonly ILogger<GroqCloudProvider> _logger;

        public GroqCloudProvider([FromKeyedServices(ApiKeyPoolKey.Groq)] IApiKeyRotator rotator,
                                IOptions<GroqConfig> options,
                                ILogger<GroqCloudProvider> logger)
        {
            _rotator = rotator;
            _config = options.Value;
            _logger = logger;
        }

        public async Task<string> AskAsync(string system, string user, CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(system),
                new UserChatMessage(user)
            };

            while (true)
            {
                try
                {
                    var key = _rotator.GetCurrentKey();
                    var chatClient = _clientCache.GetOrAdd(key, k =>
                        new ChatClient(
                            model: _config.Model,
                            credential: new ApiKeyCredential(k),
                            options: new OpenAIClientOptions { Endpoint = new Uri(_config.Url) }));

                    var response = await chatClient.CompleteChatAsync(
                        messages: messages,
                        cancellationToken: cancellationToken);

                    return response.Value.Content.Count > 0 ?
                        response.Value.Content[0].Text.ToString() :
                        string.Empty;
                }
                catch (ClientResultException ex) when (ex.Status == 429)
                {
                    var key = _rotator.GetCurrentKey(); // Lấy key hiện tại để báo cáo.
                    _rotator.ReportRateLimited(key);
                    // Loop lại để thử key tiếp theo. Nếu không còn key nào, ReportRateLimited sẽ
                    // cập nhật _currentIndex rồi GetCurrentKey() sẽ throw AllApiKeysRateLimitedException.
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
        }
    }
}
