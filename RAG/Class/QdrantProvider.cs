using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class
{
    public class QdrantProvider : IQdrantProvider
    {
        private readonly QdrantClient _qdrantClient;
        private readonly QDrantConfig _cfg;

        public QdrantProvider(QdrantClient qdrantClient, IOptions<QDrantConfig> options)
        {
            _qdrantClient = qdrantClient;
            _cfg = options.Value;
        }

        /// <summary>
        /// Kiểm tra nếu Collection chưa tồn tại thì tự động tạo mới với số Dim cấu hình sẵn
        /// </summary>
        public async Task EnsureCollectionExistsAsync(ulong dimension, CancellationToken cancellationToken = default)
        {
            string collectionName = _cfg.Collection;

            // Lấy danh sách các collection hiện tại
            var collections = await _qdrantClient.ListCollectionsAsync(cancellationToken);

            if (!collections.Contains(collectionName))
            {
                // Tạo mới collection với khoảng cách Cosine (Khuyên dùng cho văn bản)
                await CreateCollectionAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Thêm hoặc cập nhật (Upsert) một Vector kèm theo dữ liệu chữ (Payload) vào Qdrant
        /// </summary>
        public async Task UpsertVectorAsync(IEnumerable<QdrantPointInput> pointsData, 
            CancellationToken cancellationToken = default)
        {
            // 1. Khởi tạo một danh sách để chứa tất cả các Point cấu trúc của Qdrant
            var pointsToUpsert = new List<PointStruct>();

            // 2. Duyệt qua từng dữ liệu đầu vào để đóng gói
            foreach (var data in pointsData)
            {
                var point = new PointStruct
                {
                    Id = data.Id,
                    Vectors = data.Vector,
                };

                // Gắn payload và giữ nguyên định dạng gốc (int, bool, string...)
                foreach (var kvp in data.Payload)
                {
                    if (kvp.Value != null)
                    {
                        point.Payload[kvp.Key] = kvp.Value switch
                        {
                            string s => s,
                            int i => i,
                            long l => l,
                            bool b => b,
                            double d => d,
                            _ => kvp.Value.ToString() ?? string.Empty
                        };
                    }
                }

                // Thêm point đã đóng gói vào danh sách tổng
                pointsToUpsert.Add(point);
            }

            // Nếu danh sách trống thì không cần gọi API mất thời gian
            if (!pointsToUpsert.Any()) return;

            // 3. Bắn CẢ DANH SÁCH này sang Qdrant qua 1 request duy nhất
            await _qdrantClient.UpsertAsync(
                _cfg.Collection,
                pointsToUpsert,
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Tìm kiếm các Vector có độ tương đồng cao nhất dựa trên Vector câu hỏi
        /// </summary>
        public async Task<List<ScoredPointResult>> SearchVectorsAsync(float[] queryVector, Filter? filter = null, int limit = 5, CancellationToken cancellationToken = default)
        {
            // Thực hiện tìm kiếm chuyên sâu trên Qdrant
            var searchResults = await _qdrantClient.SearchAsync(
                collectionName: _cfg.Collection,
                vector: queryVector,
                filter: filter,
                limit: ulong.Parse(limit.ToString()),
                cancellationToken: cancellationToken
            );

            var results = new List<ScoredPointResult>();

            foreach (var hit in searchResults)
            {
                // Bóc tách payload dạng Protobuf Value về Dictionary<string, string> cho dễ xài
                var payloadData = hit.Payload.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.StringValue
                );

                // Chuyển đổi ID từ cấu trúc Qdrant về Guid của .NET
                Guid id = hit.Id.Uuid != null ? Guid.Parse(hit.Id.Uuid) : Guid.NewGuid();

                results.Add(new ScoredPointResult(id, hit.Score, payloadData));
            }

            return results;
        }

        public async Task CreateCollectionAsync(CancellationToken cancellationToken = default)
        {
            await _qdrantClient.CreateCollectionAsync(_cfg.Collection, new VectorParams
            {
                Size = (ulong)_cfg.Dimensions,
                Distance = Distance.Cosine
            }, cancellationToken: cancellationToken);

            await _qdrantClient.CreatePayloadIndexAsync(
                collectionName: _cfg.Collection,
                fieldName: "npcNames",
                schemaType: PayloadSchemaType.Text,
                indexParams: new PayloadIndexParams
                {
                    TextIndexParams = new TextIndexParams
                    {
                        Tokenizer = TokenizerType.Word,
                        Lowercase = true,
                        PhraseMatching = true
                    }
                },
                cancellationToken: cancellationToken
            );
        }

        public record QdrantPointInput(Guid Id, float[] Vector, Dictionary<string, object> Payload);
    }
}
