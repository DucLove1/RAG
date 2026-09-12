using OpenAI;
using OpenAI.Chat;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using System.Collections.Concurrent;
using System.ClientModel;
using Microsoft.Extensions.Options;

// ReasoningEffortLevel của OpenAI SDK đang gắn nhãn thử nghiệm; chỉ tắt cảnh báo trong adapter Groq này.
#pragma warning disable OPENAI001

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

        public int MaxOutputTokens => _config.MaxOutputTokens;

        public async Task<string> AskAsync(string system, string user, string? model = null, CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(system),
                new UserChatMessage(user)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = _config.Temperature,
                MaxOutputTokenCount = _config.MaxOutputTokens,
                ReasoningEffortLevel = _config.ReasoningEffort is { } effort ? ToSdkLevel(effort) : null
            };

            while (true)
            {
                try
                {
                    var key = _rotator.GetCurrentKey();
                    var effectiveModel = string.IsNullOrWhiteSpace(model) ? _config.Model : model;

                    // Model bake vào ChatClient lúc khởi tạo, nên khóa cache phải gồm cả model:
                    // cache theo mình API key thì consumer thứ hai sẽ dùng lại client của model thứ nhất.
                    var chatClient = _clientCache.GetOrAdd($"{key}|{effectiveModel}", _ =>
                        new ChatClient(
                            model: effectiveModel,
                            credential: new ApiKeyCredential(key),
                            options: new OpenAIClientOptions { Endpoint = new Uri(_config.Url) }));

                    var response = await chatClient.CompleteChatAsync(
                        messages: messages,
                        options: options,
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

        private static ChatReasoningEffortLevel ToSdkLevel(GroqReasoningEffort effort) => effort switch
        {
            GroqReasoningEffort.None => ChatReasoningEffortLevel.None,
            GroqReasoningEffort.Default => new ChatReasoningEffortLevel(GroqApiDefaults.ReasoningEffortDefault),
            GroqReasoningEffort.Low => ChatReasoningEffortLevel.Low,
            GroqReasoningEffort.Medium => ChatReasoningEffortLevel.Medium,
            GroqReasoningEffort.High => ChatReasoningEffortLevel.High,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, null)
        };
    }
}
