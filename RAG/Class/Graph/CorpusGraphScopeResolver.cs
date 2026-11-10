using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Graph
{
    /// <summary>
    /// Dựng <see cref="GraphScope"/> từ tiền tố <c>[stage:...]</c> trong corpus, theo luật tích lũy
    /// của <see cref="StageCatalog.IsRevealedAt"/>: ở stage n thì biết mọi dòng có stage ≤ n.
    /// <para>
    /// Đọc stage qua <see cref="ICorpusIngestionService.PreviewAsync"/> — ĐÚNG đường cắt dòng mà
    /// Qdrant được nạp theo — chứ không tự phân tích lại file. Hai bộ đếm dòng là hai ý kiến về việc
    /// <c>23_johny#L9</c> là dòng nào, và khi chúng lệch nhau thì đồ thị giấu nhầm dòng mà không ai
    /// biết.
    /// </para>
    /// <para>
    /// Chỉ giữ trong RAM những dòng CÓ gắn stage (vài chục mã), và chỉ đọc corpus khi request thật sự
    /// gửi stage. Không cache lần đọc hỏng: corpus vắng một lần mà bị đóng băng thì hết thời hạn cache
    /// vẫn suy biến dù file đã trở lại.
    /// </para>
    /// </summary>
    public sealed class CorpusGraphScopeResolver : IGraphScopeResolver
    {
        private readonly ICorpusIngestionService _corpus;
        private readonly ILogger<CorpusGraphScopeResolver> _logger;
        private readonly long _ttlMilliseconds;

        /// <summary>Thay nguyên khối, không sửa tại chỗ: request đang đọc bảng cũ không bao giờ thấy bảng dở dang.</summary>
        private volatile StageIndex? _index;

        public CorpusGraphScopeResolver(ICorpusIngestionService corpus,
                                        IOptions<GraphSearchConfig> options,
                                        ILogger<CorpusGraphScopeResolver> logger)
        {
            _corpus = corpus;
            _logger = logger;
            _ttlMilliseconds = (long)TimeSpan.FromMinutes(options.Value.StageIndexCacheMinutes).TotalMilliseconds;
        }

        public async Task<GraphScope?> ResolveAsync(string npcName,
                                                    string? stage,
                                                    CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(stage))
                return GraphScope.Unrestricted(npcName);

            var index = await GetIndexAsync(cancellationToken);

            if (index is null)
                return null;

            var hidden = index.StageByCode
                .Where(entry => !StageCatalog.IsRevealedAt(entry.Value, stage))
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);

            return new GraphScope(npcName, stage, hidden);
        }

        private async Task<StageIndex?> GetIndexAsync(CancellationToken cancellationToken)
        {
            // TickCount64: đơn điệu, chỉnh đồng hồ máy không làm bảng hết hạn sớm hay sống mãi.
            var now = Environment.TickCount64;
            var current = _index;

            if (current is not null && current.ExpiresAt > now)
                return current;

            try
            {
                var preview = await _corpus.PreviewAsync(cancellationToken: cancellationToken);

                var stageByCode = preview
                    .SelectMany(document => document.Chunks)
                    .Where(chunk => chunk.Stage is not null && chunk.ChunkCode.Length > 0)
                    .ToDictionary(chunk => chunk.ChunkCode, chunk => chunk.Stage!, StringComparer.Ordinal);

                var fresh = new StageIndex(stageByCode, now + _ttlMilliseconds);
                _index = fresh;

                return fresh;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Bảng cũ còn đó thì dùng tạm: nó đúng tới lần sửa corpus gần nhất, còn suy biến thì
                // tắt hẳn nhánh đồ thị cho mọi request có stage.
                if (current is not null)
                {
                    _logger.LogWarning(exception, "Đọc lại stage của corpus thất bại; dùng tạm bảng cũ.");
                    return current;
                }

                _logger.LogWarning(exception,
                    "Không đọc được stage của corpus; nhánh đồ thị suy biến cho các request có gửi stage.");
                return null;
            }
        }

        private sealed record StageIndex(IReadOnlyDictionary<string, string> StageByCode, long ExpiresAt);
    }
}
