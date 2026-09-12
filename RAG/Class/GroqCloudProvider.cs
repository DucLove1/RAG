using OpenAI;
using OpenAI.Chat;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using System.Collections.Concurrent;
using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

// ReasoningEffortLevel của OpenAI SDK đang gắn nhãn thử nghiệm; chỉ tắt cảnh báo trong adapter Groq này.
#pragma warning disable OPENAI001

namespace RAG.Class
{
    public class GroqCloudProvider : ILLMProvider, ILLMStreamProvider
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
            var messages = BuildMessages(system, user);
            var options = BuildOptions();

            while (true)
            {
                try
                {
                    var key = _rotator.GetCurrentKey();

                    var response = await ResolveClient(key, model).CompleteChatAsync(
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

        /// <summary>
        /// Bản streaming. Cố ý KHÔNG dùng lại thân của <see cref="AskAsync"/>: mẫu
        /// <c>while(true) + try/catch + return</c> ở trên KHÔNG bê sang iterator được, vì C# cấm
        /// <c>yield return</c> bên trong một <c>try</c> có <c>catch</c>.
        /// <para>
        /// Cái bẫy thật sự nằm sâu hơn một tầng: <c>CompleteChatStreamingAsync</c> trả về ĐỒNG BỘ
        /// và chưa gửi request nào — request chỉ bay đi ở lần <c>MoveNextAsync</c> đầu tiên. Nghĩa
        /// là 429 KHÔNG nổ ra ở chỗ gọi mà nổ bên trong vòng duyệt, đúng chỗ không đặt được
        /// <c>catch</c>. Lời giải là tự lái enumerator và tách "lấy mảnh đầu" (có
        /// <c>try/catch</c>, nằm trong một method riêng) khỏi "bơm phần còn lại" (có
        /// <c>yield</c>, không có <c>catch</c> nào).
        /// </para>
        /// <para>
        /// Hệ quả quan trọng và có chủ đích: vì không có <c>yield</c> nào trước khi mảnh đầu về
        /// tay, TOÀN BỘ việc mở kết nối chạy trong lần <c>MoveNextAsync</c> đầu tiên của consumer
        /// — tức là trước khi tầng HTTP ghi byte nào. Nhờ vậy
        /// <see cref="AllApiKeysRateLimitedException"/> vẫn trở thành một 429 thật kèm
        /// ProblemDetails, thay vì tụt xuống thành một sự kiện lỗi trong thân một response 200.
        /// </para>
        /// </summary>
        public async IAsyncEnumerable<string> AskStreamAsync(string system,
                                                             string user,
                                                             string? model = null,
                                                             [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(_config.StreamTimeoutSeconds));

            var messages = BuildMessages(system, user);
            var options = BuildOptions();

            // GIAI ĐOẠN 1 — mở luồng. Giai đoạn DUY NHẤT còn xoay key được: 429 của Groq nằm ở
            // status line của response, tức là trước delta đầu tiên. Sau khi delta đầu đã ra dây
            // thì không retry được nữa — gửi lại prompt nghĩa là người chơi thấy chữ lặp lại.
            IAsyncEnumerator<StreamingChatCompletionUpdate> updates;
            bool hasCurrent;

            while (true)
            {
                var key = _rotator.GetCurrentKey();

                updates = ResolveClient(key, model)
                    .CompleteChatStreamingAsync(messages, options, deadline.Token)
                    .GetAsyncEnumerator(deadline.Token);

                var opened = await TryAdvanceFirstAsync(updates, key);

                if (opened is { } advanced)
                {
                    hasCurrent = advanced;
                    break;
                }
            }

            // GIAI ĐOẠN 2 — bơm token. Không có catch nào ở đây nên yield return hợp lệ.
            // (await using biên dịch thành try/finally, mà yield return trong try/FINALLY thì được
            // phép — chỉ try/CATCH mới cấm.)
            await using (updates)
            {
                while (hasCurrent)
                {
                    foreach (var part in updates.Current.ContentUpdate)
                        if (part.Text is { Length: > 0 } text)
                            yield return text;

                    hasCurrent = await updates.MoveNextAsync();
                }
            }
        }

        /// <summary>
        /// Bọc DUY NHẤT lần <c>MoveNextAsync</c> đầu tiên trong <c>try/catch</c>. Phải là method
        /// riêng vì nơi gọi nó là một iterator, mà iterator không được có <c>catch</c> — đây chính
        /// là cách giữ nguyên vẹn cơ chế xoay key của đường không streaming.
        /// </summary>
        /// <returns>
        /// <c>true</c> có mảnh đầu, <c>false</c> luồng rỗng, <c>null</c> gặp 429 — key đã được báo
        /// cáo và enumerator đã dispose, caller chỉ việc lặp lại với key kế tiếp.
        /// </returns>
        private async Task<bool?> TryAdvanceFirstAsync(IAsyncEnumerator<StreamingChatCompletionUpdate> updates, string key)
        {
            try
            {
                return await updates.MoveNextAsync();
            }
            catch (ClientResultException ex) when (ex.Status == 429)
            {
                await updates.DisposeAsync();

                // Báo cáo ĐÚNG key vừa dùng, đã bắt vào biến cục bộ ở nơi gọi. Không gọi lại
                // GetCurrentKey() như đường không streaming đang làm: dưới tải song song, key hiện
                // hành có thể đã bị một request khác đẩy sang cái khác, và khi đó ta đánh dấu nhầm
                // một key còn tốt là đang bị giới hạn.
                _rotator.ReportRateLimited(key);
                return null;
            }
        }

        private List<ChatMessage> BuildMessages(string system, string user) =>
        [
            new SystemChatMessage(system),
            new UserChatMessage(user)
        ];

        private ChatCompletionOptions BuildOptions() => new()
        {
            Temperature = _config.Temperature,
            MaxOutputTokenCount = _config.MaxOutputTokens,
            ReasoningEffortLevel = _config.ReasoningEffort is { } effort ? ToSdkLevel(effort) : null
        };

        /// <summary>
        /// Model bake vào ChatClient lúc khởi tạo, nên khóa cache phải gồm cả model:
        /// cache theo mình API key thì consumer thứ hai sẽ dùng lại client của model thứ nhất.
        /// </summary>
        private ChatClient ResolveClient(string key, string? model)
        {
            var effectiveModel = string.IsNullOrWhiteSpace(model) ? _config.Model : model;

            return _clientCache.GetOrAdd($"{key}|{effectiveModel}", _ =>
                new ChatClient(
                    model: effectiveModel,
                    credential: new ApiKeyCredential(key),
                    options: new OpenAIClientOptions { Endpoint = new Uri(_config.Url) }));
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
