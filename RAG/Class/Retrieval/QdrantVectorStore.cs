using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Retrieval
{
    /// <summary>
    /// Cài đặt <see cref="IVectorStore"/> trên Qdrant.
    /// <para>
    /// Ngoài composition root (nơi phải dựng <c>QdrantClient</c>), đây là nơi DUY NHẤT import
    /// <c>Qdrant.Client.Grpc</c>. Muốn đổi sang kho vector khác thì chỉ phải viết một lớp song song
    /// với lớp này, không đụng tới pipeline.
    /// </para>
    /// </summary>
    public sealed class QdrantVectorStore : IVectorStore, IDisposable
    {
        private readonly QdrantClient _client;
        private readonly QDrantConfig _config;
        private readonly ILogger<QdrantVectorStore> _logger;

        /// <summary>Nối tiếp các lần kiểm tra collection để hai request đồng thời không cùng gọi ra mạng.</summary>
        private readonly SemaphoreSlim _ensureLock = new(1, 1);

        /// <summary>
        /// Đã xác nhận collection tồn tại hay chưa.
        /// <para>
        /// Bản trước gọi <c>ListCollectionsAsync</c> ở MỌI lần được gọi — một round-trip mạng thừa
        /// cho 100% traffic. Collection không tự biến mất giữa chừng nên một lần xác nhận là đủ.
        /// </para>
        /// Chỉ đặt <c>true</c> khi THÀNH CÔNG: nhớ lại một lần thất bại sẽ khiến mọi lần sau bỏ qua
        /// việc tạo collection.
        /// </summary>
        private volatile bool _ensured;

        public QdrantVectorStore(QdrantClient client,
                                 IOptions<QDrantConfig> options,
                                 ILogger<QdrantVectorStore> logger)
        {
            _client = client;
            _config = options.Value;
            _logger = logger;
        }

        public void Dispose() => _ensureLock.Dispose();

        public async Task EnsureCollectionExistsAsync(ulong dimension, CancellationToken cancellationToken = default)
        {
            if (_ensured)
                return;

            await _ensureLock.WaitAsync(cancellationToken);
            try
            {
                // Kiểm tra lại sau khi giành được khóa: request khác có thể vừa làm xong việc này.
                if (_ensured)
                    return;

                var collections = await _client.ListCollectionsAsync(cancellationToken);

                if (!collections.Contains(_config.Collection))
                    await CreateCollectionAsync(dimension, cancellationToken);

                _ensured = true;
            }
            finally
            {
                _ensureLock.Release();
            }
        }

        public async Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
        {
            var points = new List<PointStruct>();

            foreach (var record in records)
            {
                var point = new PointStruct
                {
                    Id = record.Id,
                    Vectors = record.Vector
                };

                // Giữ nguyên kiểu gốc của payload (int, bool, string...) thay vì ép hết về chuỗi.
                foreach (var (key, value) in record.Payload)
                {
                    if (value is null)
                        continue;

                    point.Payload[key] = value switch
                    {
                        string s => s,
                        int i => i,
                        long l => l,
                        bool b => b,
                        double d => d,
                        _ => value.ToString() ?? string.Empty
                    };
                }

                points.Add(point);
            }

            // Danh sách rỗng thì không cần gọi ra mạng.
            if (points.Count == 0)
                return;

            await _client.UpsertAsync(_config.Collection, points, cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyList<VectorHit>> SearchAsync(float[] queryVector,
                                                                VectorSearchFilter filter,
                                                                int topK,
                                                                CancellationToken cancellationToken = default)
        {
            var hits = await _client.SearchAsync(
                collectionName: _config.Collection,
                vector: queryVector,
                filter: Translate(filter),
                limit: (ulong)Math.Max(0, topK),
                cancellationToken: cancellationToken);

            var results = new List<VectorHit>(hits.Count);

            foreach (var hit in hits)
            {
                // Bản trước sinh Guid.NewGuid() khi không đọc được id — tức là BỊA ra dữ liệu để giấu
                // một điểm hỏng. Bỏ hẳn point đó và ghi log thì lỗi còn có cơ hội được nhìn thấy.
                if (!Guid.TryParse(hit.Id?.Uuid, out var id))
                {
                    _logger.LogWarning("Bỏ qua một kết quả Qdrant không có id dạng UUID hợp lệ.");
                    continue;
                }

                var payload = hit.Payload.ToDictionary(entry => entry.Key, entry => entry.Value.StringValue);

                results.Add(new VectorHit(id, hit.Score, payload));
            }

            return results;
        }

        public async Task CreateCollectionAsync(ulong dimension, CancellationToken cancellationToken = default)
        {
            await _client.CreateCollectionAsync(_config.Collection, new VectorParams
            {
                Size = dimension,
                Distance = Parse(_config.Distance, Distance.Cosine)
            }, cancellationToken: cancellationToken);

            await _client.CreatePayloadIndexAsync(
                collectionName: _config.Collection,
                fieldName: PayloadFields.NpcNames,
                schemaType: PayloadSchemaType.Text,
                indexParams: new PayloadIndexParams
                {
                    TextIndexParams = new TextIndexParams
                    {
                        Tokenizer = Parse(_config.NpcNameIndex.Tokenizer, TokenizerType.Word),
                        Lowercase = _config.NpcNameIndex.Lowercase,
                        PhraseMatching = _config.NpcNameIndex.PhraseMatching
                    }
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Đã tạo collection {Collection} với {Dimension} chiều, độ đo {Distance}.",
                _config.Collection, dimension, _config.Distance);
        }

        /// <summary>
        /// Dịch bộ lọc trung lập của ứng dụng sang <see cref="Filter"/> của Qdrant.
        /// Kiểu khớp lấy từ cấu hình, nên đổi từ phrase sang keyword không phải sửa code.
        /// </summary>
        private Filter? Translate(VectorSearchFilter filter)
        {
            if (filter.Must.Count == 0)
                return null;

            var translated = new Filter();

            foreach (var condition in filter.Must)
            {
                translated.Must.Add(_config.FilterMode switch
                {
                    PayloadFilterMode.Keyword => Conditions.MatchKeyword(condition.Field, condition.Value),
                    PayloadFilterMode.Text => Conditions.MatchText(condition.Field, condition.Value),
                    _ => Conditions.MatchPhrase(condition.Field, condition.Value)
                });
            }

            return translated;
        }

        private static TEnum Parse<TEnum>(string value, TEnum fallback) where TEnum : struct, Enum =>
            Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }
}
