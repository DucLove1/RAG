using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;
using System.Text.Json;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Lưu câu mẫu thêm lúc chạy ra một file JSON riêng, tách hẳn khỏi appsettings.json.
    /// Ghi theo kiểu atomic (file tạm rồi đổi tên) để tiến trình chết giữa chừng không để lại file cụt.
    /// </summary>
    public sealed class FileRouteUtteranceStore : IRouteUtteranceStore
    {
        private readonly SemanticRouterConfig _config;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<FileRouteUtteranceStore> _logger;

        public FileRouteUtteranceStore(IOptions<SemanticRouterConfig> options,
                                       IHostEnvironment environment,
                                       ILogger<FileRouteUtteranceStore> logger)
        {
            _config = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IReadOnlyList<StoredUtterance>> LoadAsync(CancellationToken cancellationToken = default)
        {
            var path = ResolvePath();

            try
            {
                if (!File.Exists(path))
                    return Array.Empty<StoredUtterance>();

                await using var stream = File.OpenRead(path);
                var loaded = await JsonSerializer.DeserializeAsync<List<StoredUtterance>>(stream, cancellationToken: cancellationToken);

                return loaded ?? (IReadOnlyList<StoredUtterance>)Array.Empty<StoredUtterance>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được kho câu mẫu tại {Path}, coi như rỗng.", path);
                return Array.Empty<StoredUtterance>();
            }
        }

        public async Task<bool> SaveAsync(IReadOnlyList<StoredUtterance> utterances, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath();

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var temporaryPath = path + ".tmp";

                await using (var stream = File.Create(temporaryPath))
                {
                    await JsonSerializer.SerializeAsync(stream, utterances, cancellationToken: cancellationToken);
                }

                File.Move(temporaryPath, path, overwrite: true);

                _logger.LogInformation("Đã lưu {Count} câu mẫu bổ sung vào {Path}.", utterances.Count, path);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không lưu được kho câu mẫu vào {Path}.", path);
                return false;
            }
        }

        private string ResolvePath() => AppDataPath.Resolve(_environment, _config.UtteranceStorePath);
    }
}
