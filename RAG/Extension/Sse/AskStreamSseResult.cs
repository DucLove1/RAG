using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Extension.Sse
{
    /// <summary>
    /// Đưa một luồng <see cref="AskStreamEvent"/> ra dây dưới dạng Server-Sent Events.
    /// <para>
    /// Đây là file DUY NHẤT trong repo chạm <c>Response.Body</c>, header SSE và <c>FlushAsync</c> —
    /// cùng luật cô lập mà <c>QdrantVectorStore</c> áp cho Qdrant, <c>RedisSemanticAnswerCache</c>
    /// áp cho Redis và <c>FaissSemanticAnswerCache</c> áp cho FAISS. Muốn đổi sang một giao thức
    /// khác (NDJSON, WebSocket) thì chỉ phải viết một lớp song song với lớp này.
    /// </para>
    /// <para>
    /// RANH GIỚI LỖI — toàn bộ nằm ở mệnh đề <c>when (started)</c> phía dưới:
    /// </para>
    /// <list type="table">
    /// <item><term>Chưa ghi byte nào</term><description>KHÔNG bắt exception. Để nó bay lên
    /// <c>UseExceptionHandler</c> và trở thành 429/503/500 kèm ProblemDetails, y hệt
    /// <c>api/query/ask</c>.</description></item>
    /// <item><term>Đã ghi byte</term><description>Mã HTTP không sửa được nữa, nên lỗi đi vào THÂN
    /// dưới dạng một sự kiện <c>error</c>, rồi đóng luồng.</description></item>
    /// <item><term>Client tự ngắt</term><description>Không ghi gì, không ném — không còn ai ở đầu
    /// bên kia.</description></item>
    /// </list>
    /// <para>
    /// Cột "chưa ghi byte nào" luôn đúng là một bảo đảm CẤU TRÚC chứ không phải may mắn: lớp này
    /// không gửi gì — kể cả header — cho tới khi sự kiện đầu tiên về tay, mà nguồn sự kiện là một
    /// iterator, nên toàn bộ pipeline (kể cả việc mở kết nối tới LLM) chạy trong lần
    /// <c>MoveNextAsync</c> đầu tiên.
    /// </para>
    /// </summary>
    public sealed class AskStreamSseResult : IActionResult
    {
        /// <summary>
        /// <para>
        /// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> chứ không phải bộ mã hóa mặc
        /// định. Mặc định escape MỌI ký tự ngoài ASCII thành <c>\uXXXX</c>: mỗi chữ cái tiếng Việt
        /// có dấu phình từ 2-3 byte UTF-8 lên 6 byte ASCII, trên một luồng mà từng sự kiện đều được
        /// flush riêng — tức là trả giá băng thông ở đúng chỗ nhạy cảm nhất.
        /// </para>
        /// <para>
        /// Rủi ro của bản "Unsafe" là XSS khi JSON được nhét THẲNG vào HTML. Client ở đây là Unity
        /// vẽ vào TextMeshPro, không có DOM nào để chạy script. Và nó VẪN escape dấu nháy,
        /// backslash cùng toàn bộ ký tự điều khiển, nên tính hợp lệ của JSON không hề bị nới lỏng.
        /// </para>
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly IAsyncEnumerable<AskStreamEvent> _events;
        private readonly IRagErrorMapper _errorMapper;
        private readonly ILogger _logger;

        /// <summary>Nửa cao của một cặp surrogate bị cắt rời ở cuối mảnh trước. Xem <see cref="WriteTokenAsync"/>.</summary>
        private char _pendingHighSurrogate;

        public AskStreamSseResult(IAsyncEnumerable<AskStreamEvent> events,
                                  IRagErrorMapper errorMapper,
                                  ILogger logger)
        {
            _events = events;
            _errorMapper = errorMapper;
            _logger = logger;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            var aborted = context.HttpContext.RequestAborted;

            var started = false;

            try
            {
                await foreach (var streamEvent in _events.WithCancellation(aborted))
                {
                    if (!started)
                    {
                        StartStream(response);
                        started = true;
                    }

                    switch (streamEvent)
                    {
                        case AskStreamMetaEvent meta:
                            await WriteEventAsync(response, AskStreamEventNames.Meta, meta, aborted);
                            break;

                        case AskStreamTokenEvent token:
                            await WriteTokenAsync(response, token.Text, aborted);
                            break;

                        default:
                            // done và error là khung TRUYỀN, do chính lớp này sinh. Lõi sinh ra
                            // chúng nghĩa là ai đó đã đưa quyết định "luồng kết thúc thế nào" vào
                            // tầng nghiệp vụ — nổ ngay còn hơn để hai nơi cùng phát một sự kiện
                            // kết thúc và client nhận hai cái.
                            throw new InvalidOperationException(
                                $"Nguồn sự kiện sinh ra {streamEvent.GetType().Name}, nhưng khung kết thúc và khung lỗi " +
                                "chỉ được sinh ở tầng ghi ra dây.");
                    }
                }

                // Luồng không có sự kiện nào vẫn phải là một response SSE hợp lệ, không phải một
                // response rỗng không kiểu.
                if (!started)
                    StartStream(response);

                await WriteEventAsync(response, AskStreamEventNames.Done, new AskStreamDoneEvent(), aborted);
            }
            catch (OperationCanceledException) when (aborted.IsCancellationRequested)
            {
                // Không còn ai ở đầu bên kia. Không ghi gì, không ném — để nó nổi lên sẽ thành một
                // 500 giả, đúng điều RagExceptionHandler đang tránh cho đường không streaming.
                _logger.LogInformation("Client ngắt kết nối giữa luồng SSE.");
            }
            catch (Exception ex) when (started)
            {
                var (status, title) = _errorMapper.Map(ex);

                _logger.LogError(ex, "Luồng SSE hỏng sau khi đã phát header, trả sự kiện lỗi {Status}.", status);

                await WriteEventAsync(response, AskStreamEventNames.Error,
                                      new AskStreamErrorEvent(status, title), aborted);

                // KHÔNG ném lại. Ném lại thì UseExceptionHandler sẽ cố ghi ProblemDetails vào một
                // response đã bắt đầu: kết quả là một cảnh báo trong log và một kết nối bị cắt
                // ngang, không khung nào có nghĩa cho client.
            }
        }

        private static void StartStream(HttpResponse response)
        {
            response.ContentType = SseProtocol.ContentType;
            response.Headers.CacheControl = SseProtocol.CacheControlNoCache;
            response.Headers[SseProtocol.AccelBufferingHeader] = SseProtocol.AccelBufferingOff;

            // Tắt đệm của chính Kestrel. Không có dòng này thì FlushAsync có thể không đẩy được
            // byte nào ra socket, và cả tính năng "thành công" trong im lặng.
            response.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            // CỐ Ý không đặt Content-Length (độ dài chưa biết) và không gửi Connection: keep-alive
            // (header hop-by-hop, HTTP/2 cấm hẳn).
        }

        private async Task WriteTokenAsync(HttpResponse response, string text, CancellationToken cancellationToken)
        {
            if (_pendingHighSurrogate != '\0')
            {
                text = _pendingHighSurrogate + text;
                _pendingHighSurrogate = '\0';
            }

            // Model có thể phát một emoji mà hai nửa của cặp surrogate rơi vào HAI mảnh khác nhau.
            // Nửa cao đứng một mình sẽ bị System.Text.Json thay bằng U+FFFD — không hỏng JSON,
            // không ném, chỉ mất đúng một ký tự và không ai truy ngược được dấu hỏi đó về đâu.
            // Giữ nửa cao lại cho mảnh sau tốn bốn dòng.
            if (text.Length > 0 && char.IsHighSurrogate(text[^1]))
            {
                _pendingHighSurrogate = text[^1];
                text = text[..^1];

                // Cả mảnh chỉ có mỗi nửa surrogate: chưa có gì để gửi.
                if (text.Length == 0)
                    return;
            }

            await WriteEventAsync(response, AskStreamEventNames.Token, new AskStreamTokenEvent(text), cancellationToken);
        }

        private static async Task WriteEventAsync(HttpResponse response,
                                                  string eventName,
                                                  AskStreamEvent payload,
                                                  CancellationToken cancellationToken)
        {
            var frame = SseProtocol.EventPrefix + eventName + SseProtocol.LineEnd
                      + SseProtocol.DataPrefix + JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions)
                      + SseProtocol.FrameEnd;

            // Encoding.UTF8.GetBytes KHÔNG BAO GIỜ phát BOM (chỉ GetPreamble và StreamWriter mới
            // phát), nên dùng thẳng là an toàn. TUYỆT ĐỐI KHÔNG bọc Response.Body bằng
            // StreamWriter: nó sẽ nhét một BOM vào đầu thân SSE, và mọi parser đều nghẹn ở dòng
            // đầu tiên.
            await response.Body.WriteAsync(Encoding.UTF8.GetBytes(frame), cancellationToken);

            // Flush sau MỖI sự kiện là ranh giới giữa "có streaming" và "không có streaming".
            // Kestrel dồn các lần ghi vào một pipe và chỉ đẩy đi khi pipe đầy (cỡ vài KB), mà một
            // câu trả lời tiếng Việt 20 token chỉ nặng khoảng 200 byte — không flush thì toàn bộ
            // đến nơi cùng một lúc ở cuối, VÀ MỌI BÀI TEST VẪN XANH.
            await response.Body.FlushAsync(cancellationToken);
        }
    }
}
