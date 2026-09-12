using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RAG.Interface;

namespace RAG.Extension.Errors
{
    /// <summary>
    /// Biến exception chưa được xử lý thành một response ProblemDetails.
    /// <para>
    /// CHỈ chạy được khi response CHƯA bắt đầu — đó là ràng buộc của chính
    /// <c>UseExceptionHandler</c>, không phải của lớp này. Đường SSE vì vậy phải tự lo lấy phần
    /// lỗi xảy ra sau khi header đã bay đi; xem <c>AskStreamSseResult</c>. Phép ánh xạ thì dùng
    /// CHUNG qua <see cref="IRagErrorMapper"/> để hai đường không lệch câu chữ.
    /// </para>
    /// </summary>
    public sealed class RagExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly IRagErrorMapper _errorMapper;
        private readonly ILogger<RagExceptionHandler> _logger;

        public RagExceptionHandler(IProblemDetailsService problemDetailsService,
                                   IRagErrorMapper errorMapper,
                                   ILogger<RagExceptionHandler> logger)
        {
            _problemDetailsService = problemDetailsService;
            _errorMapper = errorMapper;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
                                                    Exception exception,
                                                    CancellationToken cancellationToken)
        {
            // Client tự ngắt kết nối thì không còn ai để trả lời. Ghi log ở mức thông tin
            // và dừng lại, đừng để nó nổi lên thành lỗi 500 giả.
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Client đã ngắt kết nối trước khi request hoàn tất.");
                return true;
            }

            var (status, title) = _errorMapper.Map(exception);

            _logger.LogError(exception, "Request thất bại với mã {Status}.", status);

            httpContext.Response.StatusCode = status;

            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = exception.Message
                }
            });
        }
    }
}
