using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Extension.Errors
{
    /// <summary>
    /// Ánh xạ exception của tầng nghiệp vụ sang mã HTTP đúng nghĩa.
    /// <para>
    /// Cần thiết vì <see cref="EmbeddingUnavailableException"/> và
    /// <see cref="EmbeddingRateLimitedException"/> nói lên hai điều rất khác nhau với người gọi:
    /// một cái là "hãy thử lại sau vài giây", một cái là "hãy chậm lại". Gộp cả hai thành 500
    /// thì client không có cách nào phân biệt.
    /// </para>
    /// </summary>
    public sealed class RagExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly ErrorResponseConfig _config;
        private readonly ILogger<RagExceptionHandler> _logger;

        public RagExceptionHandler(IProblemDetailsService problemDetailsService,
                                   IOptions<ErrorResponseConfig> options,
                                   ILogger<RagExceptionHandler> logger)
        {
            _problemDetailsService = problemDetailsService;
            _config = options.Value;
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

            var (status, title) = Map(exception);

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

        private (int Status, string Title) Map(Exception exception) => exception switch
        {
            EmbeddingRateLimitedException => (StatusCodes.Status429TooManyRequests, _config.RateLimitedTitle),
            AllApiKeysRateLimitedException => (StatusCodes.Status429TooManyRequests, _config.RateLimitedTitle),
            EmbeddingUnavailableException => (StatusCodes.Status503ServiceUnavailable, _config.EmbeddingUnavailableTitle),
            _ => (StatusCodes.Status500InternalServerError, _config.UnexpectedTitle)
        };
    }
}
