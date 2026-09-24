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
    public sealed class QdrantVectorStore : IVectorStore, IChunkTextLookup, IDisposable
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

            // Index KEYWORD cho mã chunk và mã tài liệu, KHÔNG phải Text: index toàn văn tách
            // "25_phap-y#L8" thành token, nên "#L8" và "#L80" có thể chung token và tra theo mã sẽ
            // trả về nhầm dòng. Keyword khớp nguyên chuỗi, đúng thứ khóa ghép cần.
            await _client.CreatePayloadIndexAsync(
                collectionName: _config.Collection,
                fieldName: PayloadFields.ChunkCode,
                schemaType: PayloadSchemaType.Keyword,
                cancellationToken: cancellationToken);

            await _client.CreatePayloadIndexAsync(
                collectionName: _config.Collection,
                fieldName: PayloadFields.DocId,
                schemaType: PayloadSchemaType.Keyword,
                cancellationToken: cancellationToken);

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
        /// Tra nguyên văn theo mã chunk, kèm bộ lọc NPC.
        /// <para>
        /// Lọc NPC ở đây là lọc LẦN HAI — đồ thị đã lọc bằng <c>duoc_biet</c> rồi. Có chủ đích: mã
        /// chunk quay về đây là dữ liệu do đồ thị chọn, và tin nó mà không lọc lại là mở một đường
        /// vòng qua bộ lọc NPC. Một mệnh đề AND rẻ hơn nhiều so với việc phải chứng minh đường vòng
        /// đó không tồn tại sau mỗi lần sửa Cypher.
        /// </para>
        /// <para>
        /// Dùng <c>ScrollAsync</c> chứ không <c>RetrieveAsync</c>: id của điểm dẫn xuất từ mã chunk
        /// nhưng đó là chi tiết của đường NẠP, và bắt đường đọc phải biết công thức băm đó là buộc
        /// hai nơi cùng giữ một bí mật. Lọc theo payload thì đường đọc chỉ cần biết mã chunk.
        /// </para>
        /// </summary>
        public async Task<IReadOnlyDictionary<string, ChunkText>> GetByCodesAsync(
            string npcName,
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken = default)
        {
            var found = new Dictionary<string, ChunkText>(StringComparer.Ordinal);

            if (codes.Count == 0)
                return found;

            var filter = new Filter();
            filter.Must.Add(Conditions.Match(PayloadFields.ChunkCode, codes.ToList()));
            filter.Must.Add(Translate(PayloadFields.NpcNames, npcName));

            var points = await _client.ScrollAsync(
                collectionName: _config.Collection,
                filter: filter,
                limit: (uint)codes.Count,
                vectorsSelector: false,
                cancellationToken: cancellationToken);

            foreach (var point in points.Result)
            {
                if (!point.Payload.TryGetValue(PayloadFields.ChunkCode, out var code))
                    continue;

                point.Payload.TryGetValue(PayloadFields.Text, out var text);
                point.Payload.TryGetValue(PayloadFields.Source, out var source);

                found[code.StringValue] = new ChunkText(
                    code.StringValue,
                    text?.StringValue ?? string.Empty,
                    source?.StringValue ?? string.Empty);
            }

            // Mã có trong đồ thị mà không có trong kho vector nghĩa là hai nhánh đã lệch nhau — hoặc
            // corpus đổi mà chưa nạp lại, hoặc luật cắt dòng đã chệch. Dòng log này là dấu hiệu duy
            // nhất của chuyện đó, nên nó phải tồn tại kể cả khi câu trả lời vẫn ra bình thường.
            if (found.Count < codes.Count)
            {
                _logger.LogDebug("Có {Missing}/{Total} mã chunk từ đồ thị không tra được văn bản cho {Npc}.",
                    codes.Count - found.Count, codes.Count, npcName);
            }

            return found;
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
                translated.Must.Add(Translate(condition.Field, condition.Value));

            return translated;
        }

        /// <summary>Một điều kiện khớp, theo kiểu khớp đang chọn trong cấu hình.</summary>
        private Condition Translate(string field, string value) => _config.FilterMode switch
        {
            PayloadFilterMode.Keyword => Conditions.MatchKeyword(field, value),
            PayloadFilterMode.Text => Conditions.MatchText(field, value),
            _ => Conditions.MatchPhrase(field, value)
        };

        private static TEnum Parse<TEnum>(string value, TEnum fallback) where TEnum : struct, Enum =>
            Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }
}
