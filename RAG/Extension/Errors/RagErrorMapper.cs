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
    public sealed class RagErrorMapper : IRagErrorMapper
    {
        private readonly ErrorResponseConfig _config;

        public RagErrorMapper(IOptions<ErrorResponseConfig> options) => _config = options.Value;

        public (int Status, string Title) Map(Exception exception) => exception switch
        {
            EmbeddingRateLimitedException => (StatusCodes.Status429TooManyRequests, _config.RateLimitedTitle),
            AllApiKeysRateLimitedException => (StatusCodes.Status429TooManyRequests, _config.RateLimitedTitle),
            EmbeddingUnavailableException => (StatusCodes.Status503ServiceUnavailable, _config.EmbeddingUnavailableTitle),
            _ => (StatusCodes.Status500InternalServerError, _config.UnexpectedTitle)
        };
    }
}
