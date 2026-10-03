using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;
using System.Net;
using System.Net.Http.Headers;

namespace RAG.Class
{
    /// <summary>
    /// Gọi thẳng REST API System One của TypeSafe bằng <see cref="IHttpClientFactory"/>.
    /// <para>
    /// Không dùng gói NuGet <c>TypeSafe.Jev</c>: tài liệu chính thức chỉ có SDK Python/JS, và SDK
    /// .NET kia hỏi Choice bằng <c>enum</c> lúc biên dịch — trong khi route ở đây nằm trong cấu hình.
    /// API chỉ có một endpoint, nên tự gọi rẻ hơn gánh một phụ thuộc.
    /// </para>
    /// </summary>
    public sealed class TypeSafeJevClient : IJevClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TypeSafeConfig _config;
        private readonly IApiKeyRotator _rotator;
        private readonly ILogger<TypeSafeJevClient> _logger;

        public TypeSafeJevClient(IHttpClientFactory httpClientFactory,
                                 IOptions<TypeSafeConfig> options,
                                 IApiKeyRotator rotator,
                                 ILogger<TypeSafeJevClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = options.Value;
            _rotator = rotator;
            _logger = logger;
        }

        public async Task<SystemOneAnswer> AskChoiceAsync(string state,
                                                          string questionId,
                                                          SystemOneQuestion question,
                                                          CancellationToken cancellationToken = default)
        {
            var payload = new SystemOneRequest
            {
                Model = _config.Model,
                State = state,
                Questions = new Dictionary<string, SystemOneQuestion> { [questionId] = question }
            };

            // Mỗi vòng một key: 429 thì đánh dấu key đó rồi thử key kế tiếp; hết key thì rotator ném.
            while (true)
            {
                string key;
                try
                {
                    key = _rotator.GetCurrentKey();
                }
                catch (AllApiKeysRateLimitedException ex)
                {
                    throw new JevUnavailableException(ex.Message, ex);
                }

                using var response = await SendAsync(payload, key, cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _rotator.ReportRateLimited(key);
                    continue;
                }

                return await ReadAnswerAsync(response, questionId, cancellationToken);
            }
        }

        private async Task<HttpResponseMessage> SendAsync(SystemOneRequest payload,
                                                          string apiKey,
                                                          CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _config.SystemOnePath)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(TypeSafeApiDefaults.AuthScheme, apiKey);

            try
            {
                return await _httpClientFactory.CreateClient(HttpClientNames.TypeSafe)
                    .SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller hủy thật: phải lan ra ngoài, không được biến thành "Jev lỗi".
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // HttpClient báo quá hạn bằng TaskCanceledException. Để nó lọt ra như một lần hủy
                // thì router sẽ ném tiếp và làm hỏng cả request thay vì fail-open.
                throw new JevUnavailableException(
                    $"Gọi Jev quá hạn {_config.TimeoutSeconds}s.", ex);
            }
            catch (Exception ex)
            {
                throw new JevUnavailableException("Lỗi mạng khi gọi Jev.", ex);
            }
        }

        private async Task<SystemOneAnswer> ReadAnswerAsync(HttpResponseMessage response,
                                                            string questionId,
                                                            CancellationToken cancellationToken)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new JevUnavailableException(
                    $"Jev trả về lỗi ({(int)response.StatusCode}): {Truncate(body)}");
            }

            SystemOneResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<SystemOneResponse>(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new JevUnavailableException("Không đọc được phản hồi của Jev.", ex);
            }

            // Phản hồi 200 nhưng thiếu đáp án vẫn là hỏng — không được để lọt xuống thành một
            // đáp án mặc định trông như hợp lệ.
            if (result?.Answers is null ||
                !result.Answers.TryGetValue(questionId, out var answer) ||
                string.IsNullOrWhiteSpace(answer.Choice))
            {
                throw new JevUnavailableException($"Phản hồi của Jev không có đáp án cho câu hỏi \"{questionId}\".");
            }

            _logger.LogDebug("Jev ({Model}) chọn {Choice} (confidence={Confidence:F3}, input_tokens={Tokens}).",
                result.Model, answer.Choice, answer.Confidence, result.Usage?.InputTokens);

            return answer;
        }

        private string Truncate(string value) =>
            value.Length <= _config.ErrorBodyLogLimit
                ? value
                : value[.._config.ErrorBodyLogLimit] + "...";
    }
}
