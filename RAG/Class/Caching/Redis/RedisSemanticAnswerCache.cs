using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using NRedisStack.Search.Literals.Enums;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using StackExchange.Redis;

namespace RAG.Class.Caching.Redis
{
    /// <summary>
    /// Cài đặt <see cref="ISemanticAnswerCache"/> trên Redis + RediSearch.
    /// <para>
    /// Ngoài composition root, đây là nơi DUY NHẤT import <c>NRedisStack</c> và
    /// <c>StackExchange.Redis</c> — cùng luật cô lập mà <c>QdrantVectorStore</c> áp cho
    /// <c>Qdrant.Client.Grpc</c>. Muốn đổi sang kho khác thì chỉ phải viết một lớp song song với
    /// lớp này, không đụng tới pipeline.
    /// </para>
    /// <para>
    /// BẤT BIẾN của cả lớp: hai method public không bao giờ ném (trừ khi chính token của caller bị
    /// hủy). Cache hỏng phải là chuyện vô hình với người chơi.
    /// </para>
    /// </summary>
    public sealed class RedisSemanticAnswerCache : ISemanticAnswerCache,
                                                   ISemanticAnswerCacheStatistics,
                                                   ISemanticAnswerCacheAdmin,
                                                   IDisposable
    {
        private readonly IRedisConnection _connection;
        private readonly SemanticAnswerCacheConfig _config;
        private readonly ILogger<RedisSemanticAnswerCache> _logger;

        /// <summary>Nối tiếp các lần kiểm tra index để hai request đồng thời không cùng gọi FT.CREATE.</summary>
        private readonly SemaphoreSlim _ensureLock = new(1, 1);

        /// <summary>
        /// Đã xác nhận index tồn tại hay chưa. Chỉ đặt <c>true</c> khi THÀNH CÔNG: nhớ lại một lần
        /// thất bại sẽ khiến mọi lần sau bỏ qua việc tạo index.
        /// <para>
        /// Khác <c>QdrantVectorStore._ensured</c> ở một điểm: cờ này còn bị đặt LẠI về <c>false</c>
        /// khi truy vấn báo index không tồn tại. Index RediSearch có thể biến mất dưới chân tiến
        /// trình (ai đó chạy FT.DROPINDEX, hoặc container bị dựng lại với volume mới) — nhớ mãi
        /// một sự thật đã hết đúng thì mọi lần tìm sau đều hỏng cho tới khi khởi động lại.
        /// </para>
        /// </summary>
        private volatile bool _indexEnsured;

        private long _hits;
        private long _misses;
        private long _writes;
        private long _errors;

        public RedisSemanticAnswerCache(IRedisConnection connection,
                                        IOptions<SemanticAnswerCacheConfig> options,
                                        ILogger<RedisSemanticAnswerCache> logger)
        {
            _connection = connection;
            _config = options.Value;
            _logger = logger;
        }

        public void Dispose() => _ensureLock.Dispose();

        public SemanticAnswerCacheStats GetStats() =>
            new(Interlocked.Read(ref _hits),
                Interlocked.Read(ref _misses),
                Interlocked.Read(ref _writes),
                Interlocked.Read(ref _errors));

        // ---------------------------------------------------------------------------------------
        // Đọc
        // ---------------------------------------------------------------------------------------

        public async Task<CachedAnswer?> TryGetAsync(SemanticAnswerQuery query, CancellationToken cancellationToken = default)
        {
            try
            {
                return await LookupAsync(query, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Người chơi đã ngắt kết nối. KHÔNG nuốt: giữ request sống tiếp chỉ để đi hết
                // đường Qdrant + LLM cho một người đã bỏ đi là đốt hạn mức API vô ích.
                throw;
            }
            catch (Exception ex)
            {
                // Bắt Exception chứ không chỉ RedisException, và đó là chủ ý: đường này còn có thể
                // ném RedisTimeoutException, RedisConnectionException, TimeoutException của
                // WaitAsync, ObjectDisposedException khi multiplexer bị dispose lúc tắt app, và cả
                // FormatException nếu định dạng điểm của RediSearch đổi. Không cái nào trong số đó
                // đáng để một câu hỏi của người chơi trả về 500.
                RecordFailure(ex, "tra cứu", query.NpcName);
                return null;
            }
        }

        private async Task<CachedAnswer?> LookupAsync(SemanticAnswerQuery query, CancellationToken cancellationToken)
        {
            if (query.QuestionVector.Length == 0)
                return null;

            var database = await _connection.TryGetDatabaseAsync(cancellationToken);
            if (database is null)
            {
                // Đếm cả lần hỏng ở TẦNG KẾT NỐI, không chỉ lần lệnh ném. Đây là dạng hỏng phổ
                // biến nhất (Redis tắt, ngắt mạch đang mở) và nó thoát sớm ở đây chứ không bao giờ
                // đi tới khối catch — không đếm thì `errors` đứng yên ở 0 trong suốt thời gian
                // Redis chết, tức là đúng cái tình huống mà con số này sinh ra để phát hiện.
                Interlocked.Increment(ref _errors);
                return null;
            }

            if (!await EnsureIndexAsync(database, query.QuestionVector.Length, cancellationToken))
                return null;

            var search = new Query(BuildKnnQuery(BuildTag(query)))
                .AddParam(AnswerCacheFields.BlobParam, ToBlob(query.QuestionVector))
                .ReturnFields(AnswerCacheFields.Answer, AnswerCacheFields.Question, AnswerCacheFields.Score)
                .Limit(0, AnswerCacheFields.NeighborCount)
                .SetSortBy(AnswerCacheFields.Score)
                .Dialect(AnswerCacheFields.Dialect)
                .Timeout(_config.ServerQueryTimeoutMs);

            // WaitAsync dừng việc CHỜ chứ không hủy lệnh đang bay — với một lệnh đọc thì vô hại,
            // reply về muộn sẽ bị bỏ. Lớp này che những kiểu treo xảy ra TRƯỚC khi lệnh vào hàng
            // đợi (DNS, bắt tay TCP, multiplexer kẹt), thứ mà TIMEOUT phía server không với tới.
            var result = await database.FT()
                .SearchAsync(_config.IndexName, search)
                .WaitAsync(TimeSpan.FromMilliseconds(_config.OperationTimeoutMs), cancellationToken);

            _connection.ReportSuccess();

            var document = result.Documents.FirstOrDefault();

            if (document is null || !TryReadSimilarity(document, out var similarity))
            {
                Interlocked.Increment(ref _misses);
                return null;
            }

            if (similarity < _config.SimilarityThreshold)
            {
                Interlocked.Increment(ref _misses);

                // Ghi lại lần TRƯỢT SÁT ngưỡng, ở mức Debug. Không có dòng này thì việc hiệu chỉnh
                // ngưỡng chỉ đi được một chiều: log của lần trúng cho biết khi nào nên NÂNG ngưỡng
                // lên, nhưng không có gì cho biết đang bỏ lỡ những cặp câu nào và ở khoảng cách
                // bao nhiêu — tức là không có căn cứ nào để HẠ xuống ngoài việc đoán.
                _logger.LogDebug(
                    "Trượt cache ngữ nghĩa cho NPC {Npc}: gần nhất là \"{CachedQuestion}\" ở {Similarity:F3}, " +
                    "dưới ngưỡng {Threshold:F3}.",
                    query.NpcName,
                    ReadField(document, AnswerCacheFields.Question),
                    similarity,
                    _config.SimilarityThreshold);

                return null;
            }

            var answer = ReadField(document, AnswerCacheFields.Answer);

            if (string.IsNullOrWhiteSpace(answer))
            {
                Interlocked.Increment(ref _misses);
                return null;
            }

            var cachedQuestion = ReadField(document, AnswerCacheFields.Question);

            if (_config.SlidingTtl)
                RefreshTtl(database, document.Id);

            Interlocked.Increment(ref _hits);

            // Log ở mức Information chứ không phải Debug, và BẮT BUỘC in ra CẢ HAI câu. Đây là cơ
            // chế DUY NHẤT phát hiện cache trúng nhầm: một entry khớp sai không sinh exception,
            // không làm rớt request, chỉ lặng lẽ trả câu trả lời của một câu hỏi khác. In mỗi độ
            // tương đồng thì vô dụng — 0,964 tự nó không nói lên nó khớp vào cái gì.
            _logger.LogInformation(
                "Trúng cache ngữ nghĩa cho NPC {Npc} (độ tương đồng {Similarity:F3}): \"{NewQuestion}\" ≈ \"{CachedQuestion}\".",
                query.NpcName, similarity, query.Question, cachedQuestion);

            return new CachedAnswer(answer!, cachedQuestion ?? string.Empty, similarity);
        }

        // ---------------------------------------------------------------------------------------
        // Ghi
        // ---------------------------------------------------------------------------------------

        public async Task SetAsync(SemanticAnswerQuery query,
                                   string answer,
                                   bool hasContext,
                                   CancellationToken cancellationToken = default)
        {
            try
            {
                await StoreAsync(query, answer, hasContext, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Không lưu được cache KHÔNG phải lý do làm hỏng một câu trả lời đã sinh xong.
                RecordFailure(ex, "ghi", query.NpcName);
            }
        }

        private async Task StoreAsync(SemanticAnswerQuery query,
                                      string answer,
                                      bool hasContext,
                                      CancellationToken cancellationToken)
        {
            if (!IsWorthCaching(query, answer, hasContext))
                return;

            var database = await _connection.TryGetDatabaseAsync(cancellationToken);
            if (database is null)
            {
                // Cùng lý do với đường đọc: hỏng ở tầng kết nối thoát sớm ở đây, không qua catch.
                Interlocked.Increment(ref _errors);
                return;
            }

            if (!await EnsureIndexAsync(database, query.QuestionVector.Length, cancellationToken))
                return;

            var key = (RedisKey)BuildDocumentKey(query);

            var entries = new[]
            {
                new HashEntry(AnswerCacheFields.Npc, BuildTag(query)),
                new HashEntry(AnswerCacheFields.Vector, ToBlob(query.QuestionVector)),
                new HashEntry(AnswerCacheFields.Question, query.Question),
                new HashEntry(AnswerCacheFields.Answer, answer)
            };

            // HSET không nhận TTL nên phải có lệnh thứ hai, nhưng cả hai đi trong một batch nên
            // vẫn chỉ là một round-trip mạng.
            var batch = database.CreateBatch();

            var write = batch.HashSetAsync(key, entries);
            var expire = batch.KeyExpireAsync(key, TimeSpan.FromHours(_config.TtlHours));

            batch.Execute();

            await Task.WhenAll(write, expire)
                      .WaitAsync(TimeSpan.FromMilliseconds(_config.OperationTimeoutMs), cancellationToken);

            _connection.ReportSuccess();
            Interlocked.Increment(ref _writes);
        }

        /// <summary>
        /// Ba tấm lọc chống nhiễm bẩn cache, tất cả đều cấu hình được.
        /// </summary>
        private bool IsWorthCaching(SemanticAnswerQuery query, string answer, bool hasContext)
        {
            if (query.QuestionVector.Length == 0 || string.IsNullOrWhiteSpace(answer))
                return false;

            // Câu cực ngắn ("ừ", "thế à") nhúng ra vector nhiễu, gần như thứ gì cũng vượt ngưỡng.
            if (query.Question.Trim().Length < _config.MinCacheableQuestionLength)
                return false;

            // Truy hồi rỗng thì LLM gần như chắc chắn trả "tôi không biết". Ghi lại là đóng băng
            // một lần Qdrant hụt thành câu trả lời chính thức cho cả một chùm câu hỏi.
            return hasContext || _config.CacheAnswersWithoutContext;
        }

        /// <summary>
        /// Gia hạn hạn dùng khi trúng, để câu hỏi nóng sống lâu còn câu nguội tự rụng.
        /// FireAndForget là hợp lệ ở ĐÂY (khác <c>SetAsync</c>): mất lệnh gia hạn chỉ khiến câu đó
        /// phải sinh lại một lần, nên không đáng để giữ đường nóng chờ thêm một round-trip.
        /// </summary>
        private void RefreshTtl(IDatabase database, string documentKey) =>
            database.KeyExpire(documentKey,
                               TimeSpan.FromHours(_config.TtlHours),
                               flags: CommandFlags.FireAndForget);

        // ---------------------------------------------------------------------------------------
        // Vận hành
        // ---------------------------------------------------------------------------------------

        public async Task<long> PurgeAsync(string npcName, string npcPersona, CancellationToken cancellationToken = default)
        {
            try
            {
                var database = await _connection.TryGetDatabaseAsync(cancellationToken);
                if (database is null || !_indexEnsured)
                    return 0;

                var search = new Query(BuildTagFilter(Tag(npcName + ' ' + npcPersona)))
                    .Limit(0, PurgeBatchSize)
                    .Dialect(AnswerCacheFields.Dialect);

                var result = await database.FT().SearchAsync(_config.IndexName, search);

                var keys = result.Documents.Select(document => (RedisKey)document.Id).ToArray();

                if (keys.Length == 0)
                    return 0;

                await database.KeyDeleteAsync(keys);

                _logger.LogInformation("Đã xoá {Count} entry cache ngữ nghĩa của NPC {Npc}.", keys.Length, npcName);

                return keys.Length;
            }
            catch (Exception ex)
            {
                // Khác hai method trên: đây là đường vận hành, người gọi là con người và cần biết
                // là lệnh xoá KHÔNG chạy được. Vẫn không ném, nhưng trả 0 kèm log ở mức lỗi.
                Interlocked.Increment(ref _errors);
                _logger.LogError(ex, "Không xoá được cache ngữ nghĩa của NPC {Npc}.", npcName);
                return 0;
            }
        }

        // ---------------------------------------------------------------------------------------
        // Index
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Đảm bảo index tồn tại. Trả về <c>false</c> khi không dựng được — caller coi như trượt.
        /// Cùng mẫu double-check của <c>QdrantVectorStore.EnsureCollectionExistsAsync</c>: index
        /// không tự biến mất giữa chừng nên một lần xác nhận là đủ cho mọi request sau đó.
        /// </summary>
        private async Task<bool> EnsureIndexAsync(IDatabase database, int dimensions, CancellationToken cancellationToken)
        {
            if (_indexEnsured)
                return true;

            await _ensureLock.WaitAsync(cancellationToken);
            try
            {
                if (_indexEnsured)
                    return true;

                var search = database.FT();

                var schema = new Schema()
                    .AddTagField(new FieldName(AnswerCacheFields.Npc))
                    .AddVectorField(new FieldName(AnswerCacheFields.Vector),
                                    _config.UseFlatIndex ? Schema.VectorField.VectorAlgo.FLAT : Schema.VectorField.VectorAlgo.HNSW,
                                    BuildVectorAttributes(dimensions))
                    .AddTextField(new FieldName(AnswerCacheFields.Question), noIndex: true)
                    .AddTextField(new FieldName(AnswerCacheFields.Answer), noIndex: true);

                var parameters = FTCreateParams.CreateParams()
                    .On(IndexDataType.HASH)
                    .Prefix(_config.KeyPrefix);

                await search.CreateAsync(_config.IndexName, parameters, schema);

                _logger.LogInformation(
                    "Đã tạo index cache ngữ nghĩa {Index} ({Algorithm}, {Dimensions} chiều, COSINE), tiền tố khóa {Prefix}.",
                    _config.IndexName,
                    _config.UseFlatIndex ? "FLAT" : "HNSW",
                    dimensions,
                    _config.KeyPrefix);

                _indexEnsured = true;
                return true;
            }
            catch (RedisServerException ex) when (IsAlreadyExists(ex))
            {
                // Một tiến trình khác (hoặc lần chạy trước) đã tạo xong. Không phải lỗi.
                _indexEnsured = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure(ex, "tạo index", _config.IndexName);
                return false;
            }
            finally
            {
                _ensureLock.Release();
            }
        }

        private Dictionary<string, object> BuildVectorAttributes(int dimensions)
        {
            var attributes = new Dictionary<string, object>
            {
                [VectorTypeAttribute] = Float32Type,
                [VectorDimAttribute] = dimensions,
                [VectorMetricAttribute] = CosineMetric
            };

            // M và EF_CONSTRUCTION chỉ hợp lệ với HNSW; gửi kèm khi đang dùng FLAT thì FT.CREATE
            // báo lỗi cú pháp chứ không lặng lẽ bỏ qua.
            if (!_config.UseFlatIndex)
            {
                attributes[HnswMAttribute] = _config.HnswM;
                attributes[HnswEfConstructionAttribute] = _config.HnswEfConstruction;
            }

            return attributes;
        }

        // ---------------------------------------------------------------------------------------
        // Khóa, tag, và chuỗi truy vấn
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Giá trị tag phân vùng: băm của (tên NPC + mô tả tính cách).
        /// <para>
        /// Persona phải nằm trong phân vùng vì nó do CLIENT gửi lên theo từng request và đi thẳng
        /// vào system prompt — cùng tên NPC với hai persona khác nhau là hai câu trả lời khác nhau.
        /// Gộp vào giá trị băm thay vì thêm một field TAG thứ hai: một field ít hơn để đồng bộ,
        /// và bộ lọc truy vấn cũng ngắn hơn.
        /// </para>
        /// </summary>
        private static string BuildTag(SemanticAnswerQuery query) => Tag(query.NpcName + ' ' + query.NpcPersona);

        /// <summary>
        /// Băm một giá trị phân vùng thành hex để dùng làm TAG.
        /// <para>
        /// Cú pháp TAG của RediSearch coi khoảng trắng và gần như toàn bộ dấu câu là ký tự đặc
        /// biệt phải escape, còn DẤU PHẨY thì bị hiểu là dấu ngăn giữa hai tag. Tên NPC trong game
        /// có cả khoảng trắng lẫn dấu tiếng Việt, nên đi đường escape nghĩa là phải chép đúng một
        /// bảng escape của RediSearch và giữ nó đồng bộ qua các phiên bản. Băm ra [0-9A-F] thì
        /// không còn ký tự nào cần escape, độ dài cố định, và bảng escape đó biến mất khỏi codebase.
        /// </para>
        /// <para>
        /// Chuẩn hóa Trim + ToLowerInvariant TRƯỚC khi băm: "Johny" và "johny " phải là cùng một
        /// NPC, và quyết định đó nên nằm ở đây một cách cố ý thay vì phụ thuộc vào việc client gửi
        /// lên thế nào.
        /// </para>
        /// </summary>
        private static string Tag(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())))
                   [..AnswerCacheFields.TagLength];

        /// <summary>
        /// Khóa document, tất định theo (phân vùng, câu hỏi): hỏi lại ĐÚNG câu cũ thì HSET ghi đè
        /// chứ không đẻ thêm entry.
        /// <para>
        /// Tag phải nằm trong khóa chứ không chỉ trong field: tag quyết định AI TÌM THẤY document,
        /// khóa quyết định DOCUMENT NÀO BỊ GHI ĐÈ. Khóa chỉ theo câu hỏi thì NPC B hỏi cùng câu sẽ
        /// đè lên document của NPC A và lật tag của nó — rò dữ liệu mà không bộ lọc nào cứu được.
        /// </para>
        /// <para>
        /// Không dùng GUID (mỗi lần trượt đẻ một document, tích lại thành hàng nghìn vector gần
        /// trùng làm chậm KNN mà không tăng recall) và không băm vector (float không phải danh
        /// tính ổn định — cùng câu nhúng lại có thể lệch bit cuối, lại đẻ document mới).
        /// </para>
        /// </summary>
        private string BuildDocumentKey(SemanticAnswerQuery query) =>
            $"{_config.KeyPrefix}{BuildTag(query)}:{Tag(query.Question)}";

        /// <summary>
        /// Truy vấn KNN kèm bộ lọc TRƯỚC. Phần trong ngoặc chạy trước mũi tên, nên RediSearch thu
        /// hẹp về đúng phân vùng rồi mới duyệt vector bên trong — đó là thứ làm cho việc tách theo
        /// NPC là thật chứ không phải lọc sau khi đã tìm.
        /// </summary>
        private static string BuildKnnQuery(string tag) =>
            $"({BuildTagFilter(tag)})=>[KNN {AnswerCacheFields.NeighborCount} @{AnswerCacheFields.Vector} " +
            $"${AnswerCacheFields.BlobParam} AS {AnswerCacheFields.Score}]";

        private static string BuildTagFilter(string tag) => $"@{AnswerCacheFields.Npc}:{{{tag}}}";

        /// <summary>
        /// Đóng gói vector thành FLOAT32 little-endian, đúng thứ RediSearch chờ đợi.
        /// Một lần cấp phát; idiom <c>vector.SelectMany(BitConverter.GetBytes)</c> trong tài liệu
        /// Redis cấp phát một mảng 4 byte cho MỖI chiều, tức là 768 mảng mỗi lần tra.
        /// </summary>
        private static byte[] ToBlob(float[] vector) => MemoryMarshal.AsBytes<float>(vector).ToArray();

        // ---------------------------------------------------------------------------------------
        // Đọc kết quả
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Đổi điểm của RediSearch thành độ tương đồng.
        /// </summary>
        private static bool TryReadSimilarity(Document document, out double similarity)
        {
            similarity = 0d;

            var raw = ReadField(document, AnswerCacheFields.Score);

            if (raw is null)
                return false;

            // BẮT BUỘC InvariantCulture. RediSearch trả điểm dưới dạng CHUỖI ("0.0473321676254"),
            // còn máy chạy locale vi-VN thì dấu chấm là dấu phân nhóm hàng nghìn — double.Parse sẽ
            // đọc ra 473321676254 và mọi lần so ngưỡng đều trượt. Triệu chứng duy nhất là cache
            // "không bao giờ trúng", không có một exception nào để lần theo.
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
                return false;

            // COSINE của RediSearch trả về KHOẢNG CÁCH (1 - độ tương đồng), nhỏ nhất là gần nhất.
            // Quy đổi một lần ở đây rồi phần còn lại của lớp chỉ thấy độ tương đồng, và so sánh
            // luôn viết dạng "similarity >= ngưỡng" — tương đương về đại số với
            // "distance <= 1 - ngưỡng" nhưng đọc giống hệt ngưỡng của node định tuyến, nên không
            // ai phải suy lại dấu khi chỉnh. Viết ngược dấu là lỗi âm thầm tệ nhất của cả tính
            // năng: cache sẽ trúng đúng những câu KHÁC NGHĨA NHẤT.
            similarity = 1d - distance;
            return true;
        }

        private static string? ReadField(Document document, string field)
        {
            foreach (var property in document.GetProperties())
                if (property.Key == field)
                    return property.Value.ToString();

            return null;
        }

        private void RecordFailure(Exception exception, string operation, string subject)
        {
            _connection.ReportFailure(exception);
            Interlocked.Increment(ref _errors);

            // Index có thể đã biến mất dưới chân (FT.DROPINDEX, FLUSHALL, hoặc container bị dựng
            // lại với volume mới). Nhớ mãi "đã có index" là nhớ một điều đã hết đúng — mọi lần sau
            // sẽ hỏng cho tới khi khởi động lại. Quên đi để lần gọi kế tiếp tự tạo lại.
            if (IsMissingIndex(exception))
                _indexEnsured = false;

            _logger.LogWarning(exception,
                "Cache ngữ nghĩa không dùng được khi {Operation} ({Subject}); đi tiếp đường Qdrant + LLM.",
                operation, subject);
        }

        /// <summary>
        /// Nhận diện lỗi qua thông điệp là cách duy nhất: RediSearch không trả về mã lỗi có cấu
        /// trúc mà chỉ trả một chuỗi. Các chuỗi dưới đây lấy từ thông điệp THẬT của Redis 8 —
        /// đừng thay bằng chuỗi phỏng đoán.
        /// </summary>
        private static bool IsAlreadyExists(Exception exception) =>
            Mentions(exception, "Index already exists");

        /// <summary>
        /// Index đã biến mất khỏi server. Thông điệp thật của Redis 8 là
        /// "SEARCH_INDEX_NOT_FOUND Index not found: &lt;tên&gt;"; hai chuỗi còn lại là dạng cũ, giữ lại
        /// để không phụ thuộc vào đúng một phiên bản server.
        /// </summary>
        private static bool IsMissingIndex(Exception exception) =>
            Mentions(exception, "SEARCH_INDEX_NOT_FOUND") ||
            Mentions(exception, "Index not found") ||
            Mentions(exception, "Unknown index name") ||
            Mentions(exception, "no such index");

        /// <summary>
        /// Soi cả chuỗi exception lồng nhau: lệnh của NRedisStack có thể bọc lỗi của server vào
        /// trong một exception khác, và khi đó chỉ đọc Message ngoài cùng là trượt.
        /// </summary>
        private static bool Mentions(Exception exception, string fragment)
        {
            for (var current = exception; current is not null; current = current.InnerException)
                if (current.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        // Tên thuộc tính của khai báo VECTOR trong FT.CREATE. Là từ khóa giao thức của RediSearch
        // chứ không phải cấu hình, nên nằm ở đây thay vì trong appsettings.
        private const string VectorTypeAttribute = "TYPE";
        private const string VectorDimAttribute = "DIM";
        private const string VectorMetricAttribute = "DISTANCE_METRIC";
        private const string HnswMAttribute = "M";
        private const string HnswEfConstructionAttribute = "EF_CONSTRUCTION";
        private const string Float32Type = "FLOAT32";
        private const string CosineMetric = "COSINE";

        /// <summary>Trần số entry lấy về trong một lượt xoá. Đủ lớn cho mọi phân vùng thực tế.</summary>
        private const int PurgeBatchSize = 10_000;
    }
}
