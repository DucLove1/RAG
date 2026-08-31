using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Lưu vector câu mẫu ra một file JSON để lần khởi động sau không phải gọi lại API embedding.
    /// <para>
    /// Cache là tối ưu hóa, không phải nguồn sự thật: MỌI lỗi đọc/ghi đều được nuốt và chỉ ghi log,
    /// không bao giờ được phép làm hỏng quá trình khởi động.
    /// </para>
    /// </summary>
    public sealed class FileRouteVectorCache : IRouteVectorCache
    {
        private readonly SemanticRouterConfig _config;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<FileRouteVectorCache> _logger;

        public FileRouteVectorCache(IOptions<SemanticRouterConfig> options,
                                    IHostEnvironment environment,
                                    ILogger<FileRouteVectorCache> logger)
        {
            _config = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IReadOnlyDictionary<string, float[]>?> TryLoadAsync(string fingerprint,
                                                                              CancellationToken cancellationToken = default)
        {
            var path = ResolvePath();

            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogDebug("Chưa có file cache vector tại {Path}.", path);
                    return null;
                }

                await using var stream = File.OpenRead(path);
                var payload = await JsonSerializer.DeserializeAsync<CachePayload>(stream, JsonOptions, cancellationToken);

                if (payload is null || payload.Vectors is null)
                {
                    _logger.LogWarning("File cache vector rỗng hoặc sai định dạng, sẽ nhúng lại.");
                    return null;
                }

                if (!string.Equals(payload.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Vân tay cache đã cũ (câu mẫu hoặc model đã đổi), sẽ nhúng lại.");
                    return null;
                }

                return payload.Vectors;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được cache vector tại {Path}, coi như chưa có cache.", path);
                return null;
            }
        }

        public async Task SaveAsync(string fingerprint,
                                    IReadOnlyDictionary<string, float[]> vectors,
                                    CancellationToken cancellationToken = default)
        {
            var path = ResolvePath();

            try
            {
                var payload = new CachePayload
                {
                    Fingerprint = fingerprint,
                    Vectors = vectors.ToDictionary(entry => entry.Key, entry => entry.Value)
                };

                await AtomicFileWriter.WriteAsync(path,
                    stream => JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken));

                _logger.LogInformation("Đã ghi cache vector cho {Count} câu mẫu vào {Path}.", vectors.Count, path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không ghi được cache vector vào {Path}, bỏ qua.", path);
            }
        }

        private string ResolvePath() => AppDataPath.Resolve(_environment, _config.VectorCachePath);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private sealed class CachePayload
        {
            /// <summary>
            /// Băm của model + số chiều + toàn bộ câu mẫu. Cố tình KHÔNG gồm prompt template hay ngưỡng,
            /// vì hai thứ đó không ảnh hưởng tới giá trị vector — nhờ vậy tinh chỉnh prompt/ngưỡng
            /// vẫn dùng lại được cache.
            /// </summary>
            public string Fingerprint { get; set; } = string.Empty;

            /// <summary>Khóa là chính câu mẫu, nên đổi tên route không làm mất cache.</summary>
            public Dictionary<string, float[]>? Vectors { get; set; }
        }
    }
}
