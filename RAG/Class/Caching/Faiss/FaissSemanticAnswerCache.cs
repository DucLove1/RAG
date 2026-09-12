using Faiss.Cpu.Indexes;
using Faiss.Cpu.Indexes.Mapped;
using Faiss.Cpu.Selectors;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;

namespace RAG.Class.Caching.Faiss
{
    /// <summary>
    /// Cài đặt <see cref="ISemanticAnswerCache"/> trên FAISS, chạy TRONG TIẾN TRÌNH.
    /// <para>
    /// Ngoài composition root, đây là nơi DUY NHẤT import <c>Faiss.*</c> — cùng luật cô lập mà
    /// <c>QdrantVectorStore</c> áp cho <c>Qdrant.Client.Grpc</c> và <c>RedisSemanticAnswerCache</c>
    /// áp cho <c>NRedisStack</c>. Thực tế còn chặt hơn cả hai: file đăng ký DI chỉ chạm tên lớp
    /// này, không chạm một type nào của FAISS.
    /// </para>
    /// <para>
    /// BẤT BIẾN của cả lớp: hai method public của <see cref="ISemanticAnswerCache"/> không bao giờ
    /// ném (trừ khi chính token của caller bị hủy). Cache hỏng phải là chuyện vô hình với người
    /// chơi. Ở đây không có mạng để hỏng, nhưng vẫn còn P/Invoke sang bộ nhớ native, số chiều lệch,
    /// và trần phân vùng — nên luật vẫn nguyên giá trị.
    /// </para>
    /// <para>
    /// METRIC: <c>IndexFlatIP</c> chấm bằng TÍCH VÔ HƯỚNG THUẦN, và nó CHỈ bằng cosine khi cả hai
    /// vector có độ dài 1 — điều Gemini không đảm bảo (xem <see cref="VectorMath.L2Normalize"/>).
    /// Vì vậy mọi vector đều được chuẩn hóa L2 ở biên. Nhưng con số dùng để SO NGƯỠNG và GHI LOG
    /// vẫn do <see cref="VectorMath.CosineSimilarity"/> chấm lại, chứ không lấy thẳng điểm của
    /// FAISS. Lý do: nếu điểm quyết định đến từ FAISS thì "quên chuẩn hóa" là một lỗi VÔ HÌNH trả
    /// nhầm câu; chấm lại bằng cosine đầy đủ thì dù có quên, ngưỡng vẫn so đúng và lỗi chỉ còn là
    /// thứ tự ứng viên bị nhiễu — tức là hạ từ lỗi bảo mật xuống lỗi hiệu năng, giá 768 phép
    /// nhân-cộng (dưới một micro-giây) mỗi lượt tra. Đổi lại còn được một định nghĩa "độ tương
    /// đồng" duy nhất cho cả codebase, nên ngưỡng 0.90 ở đây cùng thang với ngưỡng của node định
    /// tuyến.
    /// </para>
    /// <para>
    /// ⚠️ DẤU NGƯỢC NHAU GIỮA HAI PROVIDER: RediSearch <c>COSINE</c> trả về KHOẢNG CÁCH (nhỏ = gần)
    /// nên bản Redis phải quy đổi <c>1 - distance</c>; FAISS <c>InnerProduct</c> trả về ĐỘ TƯƠNG
    /// ĐỒNG (lớn = gần) và kết quả đã sắp xếp giảm dần. Viết ngược dấu là lỗi âm thầm tệ nhất của
    /// cả tính năng: cache sẽ trúng đúng những câu KHÁC NGHĨA NHẤT.
    /// </para>
    /// <para>
    /// API của <c>Faiss.NET.Interop 1.0.0-preview.4</c>, ghi lại từ bước spike vì binding đang
    /// alpha và nhánh master đã khác:
    /// <code>
    /// Faiss.Cpu.Indexes.IndexFlatIP(long dimensions)
    /// Faiss.Cpu.Indexes.Mapped.IndexIDMap2&lt;T&gt;(T index, bool takeOwnership)
    ///   void Add(long count, ReadOnlySpan&lt;float&gt; vectors, ReadOnlySpan&lt;long&gt; xids)
    ///   void Search(long count, ReadOnlySpan&lt;float&gt; q, int k, Span&lt;float&gt; distances, Span&lt;long&gt; labels)
    ///   long RemoveIds(IIDSelector selector) | void Reset() | long TotalCount | int Dimensions
    /// Faiss.Cpu.Selectors.IDSelectorBatch(ReadOnlySpan&lt;long&gt; indices)
    /// </code>
    /// Hai hành vi đã đo được và cả lớp này phụ thuộc vào:
    /// (1) Add lại một <c>xid</c> ĐÃ TỒN TẠI thì <c>IndexIDMap2</c> KHÔNG ném mà ĐẺ BẢN TRÙNG —
    /// vì vậy đường ghi bắt buộc phải <c>RemoveIds</c> trước, xem <see cref="Store"/>;
    /// (2) khi k lớn hơn số vector, các chỗ trống trả về nhãn <c>-1</c> và điểm
    /// <c>float.MinValue</c> — đường đọc phải bỏ qua.
    /// </para>
    /// </summary>
    public sealed class FaissSemanticAnswerCache : ISemanticAnswerCache,
                                                   ISemanticAnswerCacheStatistics,
                                                   ISemanticAnswerCacheAdmin,
                                                   IPersistableAnswerCache,
                                                   IDisposable
    {
        /// <summary>Nhãn FAISS trả về cho một ô kết quả trống (k lớn hơn số vector trong index).</summary>
        private const long EmptyLabel = -1L;

        private readonly SemanticAnswerCacheConfig _shared;
        private readonly SemanticAnswerCacheFaissConfig _config;
        private readonly ILogger<FaissSemanticAnswerCache> _logger;

        /// <summary>Số chiều chốt từ cấu hình model nhúng. Vector lệch chiều bị từ chối, không bao giờ vào index.</summary>
        private readonly int _dimensions;

        private readonly string _fingerprint;

        /// <summary>
        /// MỘT khóa cho toàn bộ store, bảo vệ cả từ điển phân vùng lẫn mọi index bên trong.
        /// <para>
        /// Index FAISS chỉ KHÔNG an toàn khi vừa đọc vừa ghi; nhiều luồng cùng Search một
        /// <c>IndexFlat</c> là hợp lệ. <see cref="ReaderWriterLockSlim"/> khai thác đúng điều đó
        /// nên đường nóng (tra cache) không chặn nhau chút nào, trong khi một <c>lock</c> thường sẽ
        /// nối tiếp cả những lượt đọc vốn không xung đột.
        /// </para>
        /// <para>
        /// MỘT khóa chứ không phải một khóa mỗi phân vùng: <see cref="PurgeAsync"/> và
        /// <see cref="SweepExpired"/> phải sửa CẢ từ điển phân vùng LẪN index bên trong, tức là hai
        /// khóa, tức là có thứ tự khóa, tức là có deadlock để mà debug. Không đáng, vì chi phí khóa
        /// ghi ở đây nhỏ và đo được: khóa ghi chỉ được lấy MỘT lần cho mỗi lần cache TRƯỢT, mà lần
        /// trượt đó ngay sau đó tốn một lượt gọi LLM hàng trăm mili-giây — phần ghi (RemoveIds +
        /// Add một vector) chiếm dưới một phần nghìn thời gian của chính request đã kích hoạt nó.
        /// </para>
        /// </summary>
        private readonly ReaderWriterLockSlim _lock = new();

        private readonly Dictionary<string, Partition> _partitions = new(StringComparer.Ordinal);

        /// <summary>
        /// Nguồn cấp id cho FAISS. Chỉ tăng, không tái dùng, và KHÔNG được ghi xuống đĩa: id là chi
        /// tiết nội bộ của một lần chạy chứ không phải danh tính của entry (danh tính là cặp
        /// tag + questionHash). Đưa nó xuống đĩa chỉ thêm một thứ có thể lệch pha.
        /// </summary>
        private long _nextId;

        private int _entryCount;

        private long _hits;
        private long _misses;
        private long _writes;
        private long _errors;
        private long _changeCount;

        /// <summary>Chặn spam log khi chạm trần phân vùng: một cảnh báo là đủ để người vận hành biết.</summary>
        private int _partitionLimitWarned;

        public FaissSemanticAnswerCache(IOptions<SemanticAnswerCacheConfig> sharedOptions,
                                        IOptions<SemanticAnswerCacheFaissConfig> faissOptions,
                                        IOptions<GeminiEmbeddingModelConfig> embeddingOptions,
                                        ILogger<FaissSemanticAnswerCache> logger)
        {
            _shared = sharedOptions.Value;
            _config = faissOptions.Value;
            _logger = logger;

            var embedding = embeddingOptions.Value;
            _dimensions = embedding.OutputDimensions;

            // Vân tay hỏi thẳng cấu hình model nhúng chứ KHÔNG thêm một mục cấu hình số chiều riêng
            // — QDrantConfig đã bỏ hẳn trường đó vì cùng lý do: hai nguồn sự thật cho một con số là
            // một lỗi im lặng đang chờ. Thành phần "l2norm" có mặt để nếu sau này đổi quy ước chuẩn
            // hóa thì file cũ tự bị loại thay vì trộn vector hai thang với nhau.
            _fingerprint = $"{embedding.Model}|{embedding.OutputDimensions}|l2norm|RAGAC1";
        }

        public string Fingerprint => _fingerprint;

        public long ChangeCount => Interlocked.Read(ref _changeCount);

        public SemanticAnswerCacheStats GetStats() =>
            new(Interlocked.Read(ref _hits),
                Interlocked.Read(ref _misses),
                Interlocked.Read(ref _writes),
                Interlocked.Read(ref _errors));

        // ---------------------------------------------------------------------------------------
        // Đọc
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Thân method ĐỒNG BỘ, chỉ bọc lại thành <see cref="Task"/>.
        /// <para>
        /// Interface cố ý bất đồng bộ vì nó được thiết kế cho Redis đi qua MẠNG; FAISS chạy trong
        /// tiến trình nên sự bất đối xứng này là giá của việc có hai provider, và nó rẻ —
        /// <c>Miss</c> được tái dùng qua static nên đường trượt không cấp phát gì.
        /// </para>
        /// <para>
        /// TUYỆT ĐỐI KHÔNG <c>Task.Run</c>: việc cần làm là CPU-bound và dưới một mili-giây. Đẩy
        /// sang thread pool là trả thêm một lần chuyển ngữ cảnh cùng một state machine để LÀM CHẬM
        /// chính nó, và chiếm một thread của pool trong khi toàn bộ giá trị của thiết kế async ở
        /// đây là KHÔNG chiếm thread.
        /// </para>
        /// </summary>
        public Task<CachedAnswer?> TryGetAsync(SemanticAnswerQuery query, CancellationToken cancellationToken = default)
        {
            // Người chơi đã ngắt kết nối thì không có lý do gì đi tiếp Qdrant + LLM. Đây là ngoại
            // lệ DUY NHẤT được phép thoát ra khỏi method này.
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var hit = Lookup(query);
                return hit is null ? Miss : Task.FromResult<CachedAnswer?>(hit);
            }
            catch (Exception ex)
            {
                // Bắt Exception chứ không một kiểu cụ thể, và đó là chủ ý: đường này còn có thể ném
                // DllNotFoundException khi native của FAISS không nạp được, SEHException từ tầng
                // P/Invoke, và ObjectDisposedException khi index bị dispose lúc tắt app. Không cái
                // nào đáng để một câu hỏi của người chơi trả về 500.
                RecordFailure(ex, "tra cứu", query.NpcName);
                return Miss;
            }
        }

        private static readonly Task<CachedAnswer?> Miss = Task.FromResult<CachedAnswer?>(null);

        private CachedAnswer? Lookup(SemanticAnswerQuery query)
        {
            if (query.QuestionVector.Length == 0)
                return null;

            if (query.QuestionVector.Length != _dimensions)
            {
                // Không im lặng trả null: lệch số chiều nghĩa là cấu hình model nhúng và dữ liệu
                // thật đã rời nhau, và triệu chứng duy nhất còn lại sẽ là "cache không bao giờ
                // trúng" — đúng loại im lặng mà bộ đếm errors sinh ra để phá.
                RecordDimensionMismatch(query.QuestionVector.Length);
                return null;
            }

            var tag = AnswerCachePartition.Tag(query.NpcName, query.NpcPersona);
            var unit = VectorMath.L2Normalize(query.QuestionVector);

            _lock.EnterReadLock();
            try
            {
                if (!_partitions.TryGetValue(tag, out var partition) || partition.Index.TotalCount == 0)
                {
                    Interlocked.Increment(ref _misses);
                    return null;
                }

                var k = (int)Math.Min(_config.NeighborCount, partition.Index.TotalCount);

                Span<float> distances = stackalloc float[k];
                Span<long> labels = stackalloc long[k];

                partition.Index.Search(1, unit, k, distances, labels);

                var nowTicks = DateTime.UtcNow.Ticks;
                var entry = FindFirstLiveCandidate(partition, labels, nowTicks);

                if (entry is null)
                {
                    Interlocked.Increment(ref _misses);
                    return null;
                }

                // Chấm lại bằng cosine đầy đủ thay vì tin điểm của FAISS — lý do ở khối comment đầu
                // lớp. Đây là con số CHÍNH THỨC: nó quyết định trúng hay trượt và nó đi vào log.
                var similarity = VectorMath.CosineSimilarity(entry.Vector, unit);

                if (similarity < _shared.SimilarityThreshold)
                {
                    Interlocked.Increment(ref _misses);

                    // Ghi lại lần TRƯỢT SÁT ngưỡng, ở mức Debug. Không có dòng này thì việc hiệu
                    // chỉnh ngưỡng chỉ đi được một chiều: log của lần trúng cho biết khi nào nên
                    // NÂNG ngưỡng lên, nhưng không có gì cho biết đang bỏ lỡ những cặp câu nào và
                    // ở khoảng cách bao nhiêu — tức là không có căn cứ nào để HẠ xuống ngoài đoán.
                    _logger.LogDebug(
                        "Trượt cache ngữ nghĩa cho NPC {Npc}: gần nhất là \"{CachedQuestion}\" ở {Similarity:F3}, " +
                        "dưới ngưỡng {Threshold:F3}.",
                        query.NpcName, entry.Question, similarity, _shared.SimilarityThreshold);

                    return null;
                }

                if (string.IsNullOrWhiteSpace(entry.Answer))
                {
                    Interlocked.Increment(ref _misses);
                    return null;
                }

                // Gia hạn ngay dưới khóa ĐỌC. Làm được vì CacheEntry là class có hai field long cập
                // nhật bằng Interlocked — nếu entry là record bất biến thì mỗi lần TRÚNG cache đều
                // phải nâng lên khóa ghi độc quyền, biến đường nóng thành nút cổ chai. Rẻ hơn hẳn
                // bản Redis: không round-trip, không FireAndForget, không có khả năng mất lệnh.
                if (_shared.SlidingTtl)
                    Interlocked.Exchange(ref entry.ExpiresAtUtcTicks, nowTicks + TimeSpan.FromHours(_shared.TtlHours).Ticks);

                Interlocked.Exchange(ref entry.LastAccessUtcTicks, nowTicks);
                Interlocked.Increment(ref _hits);

                // Log ở mức Information chứ không phải Debug, và BẮT BUỘC in ra CẢ HAI câu. Đây là
                // cơ chế DUY NHẤT phát hiện cache trúng nhầm: một entry khớp sai không sinh
                // exception, không làm rớt request, chỉ lặng lẽ trả câu trả lời của một câu hỏi
                // khác. In mỗi độ tương đồng thì vô dụng — 0,964 tự nó không nói lên nó khớp cái gì.
                _logger.LogInformation(
                    "Trúng cache ngữ nghĩa cho NPC {Npc} (độ tương đồng {Similarity:F3}): \"{NewQuestion}\" ≈ \"{CachedQuestion}\".",
                    query.NpcName, similarity, query.Question, entry.Question);

                return new CachedAnswer(entry.Answer, entry.Question, similarity);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Ứng viên còn SỐNG đầu tiên trong danh sách láng giềng, hoặc <c>null</c>.
        /// <para>
        /// Dừng ở ứng viên sống đầu tiên chứ KHÔNG duyệt tiếp để tìm ứng viên nào vượt ngưỡng: đi
        /// tiếp là âm thầm nới lỏng recall so với provider Redis, vốn chỉ xét đúng láng giềng gần
        /// nhất. Việc lấy nhiều hơn một láng giềng chỉ để một entry ĐÃ HẾT HẠN không chặn đường một
        /// entry còn sống ngay phía sau nó — xem <c>SemanticAnswerCacheFaissConfig.NeighborCount</c>.
        /// </para>
        /// </summary>
        private static CacheEntry? FindFirstLiveCandidate(Partition partition, ReadOnlySpan<long> labels, long nowTicks)
        {
            foreach (var label in labels)
            {
                // Ô trống khi k lớn hơn số vector. Cũng chặn luôn trường hợp id có trong index mà
                // không có trong từ điển: về lý thuyết không xảy ra vì cả hai đổi dưới cùng một
                // khóa ghi, nhưng bỏ qua thì rẻ hơn nhiều so với một NullReferenceException nuốt
                // vào bộ đếm errors.
                if (label == EmptyLabel || !partition.Entries.TryGetValue(label, out var entry))
                    continue;

                if (Interlocked.Read(ref entry.ExpiresAtUtcTicks) <= nowTicks)
                    continue;

                return entry;
            }

            return null;
        }

        // ---------------------------------------------------------------------------------------
        // Ghi
        // ---------------------------------------------------------------------------------------

        public Task SetAsync(SemanticAnswerQuery query,
                             string answer,
                             bool hasContext,
                             CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Store(query, answer, hasContext);
            }
            catch (Exception ex)
            {
                // Không lưu được cache KHÔNG phải lý do làm hỏng một câu trả lời đã sinh xong.
                RecordFailure(ex, "ghi", query.NpcName);
            }

            return Task.CompletedTask;
        }

        private void Store(SemanticAnswerQuery query, string answer, bool hasContext)
        {
            if (!IsWorthCaching(query, answer, hasContext))
                return;

            if (query.QuestionVector.Length != _dimensions)
            {
                RecordDimensionMismatch(query.QuestionVector.Length);
                return;
            }

            var tag = AnswerCachePartition.Tag(query.NpcName, query.NpcPersona);
            var questionHash = AnswerCachePartition.QuestionHash(query.Question);
            var unit = VectorMath.L2Normalize(query.QuestionVector);

            _lock.EnterWriteLock();
            try
            {
                if (!_partitions.TryGetValue(tag, out var partition))
                {
                    if (_partitions.Count >= _config.MaxPartitions)
                    {
                        RecordPartitionLimitReached(query.NpcName);
                        return;
                    }

                    partition = Partition.Create(_dimensions);
                    _partitions[tag] = partition;
                }

                AddEntry(partition, new CacheEntry
                {
                    Tag = tag,
                    QuestionHash = questionHash,
                    Question = query.Question,
                    Answer = answer,
                    Vector = unit,
                    ExpiresAtUtcTicks = DateTime.UtcNow.Ticks + TimeSpan.FromHours(_shared.TtlHours).Ticks,
                    LastAccessUtcTicks = DateTime.UtcNow.Ticks
                });

                Interlocked.Increment(ref _writes);
                Interlocked.Increment(ref _changeCount);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            // Trần MaxEntries KHÔNG được ép ở đây: làm thế phải sắp xếp toàn bộ cache dưới khóa ghi
            // ngay trên đường nóng. Để nhịp quét nền lo; vượt trần tạm trong tối đa một
            // FlushIntervalSeconds là chấp nhận được với một cache.
        }

        /// <summary>
        /// Thêm một entry vào phân vùng, ghi đè entry cũ của CÙNG câu hỏi nếu có.
        /// Gọi viên PHẢI đang giữ khóa ghi.
        /// <para>
        /// Ghi đè phải làm bằng RemoveIds rồi cấp id MỚI, không phải Add lại id cũ: đã đo được là
        /// <c>IndexIDMap2</c> không ném khi gặp <c>xid</c> trùng mà ĐẺ BẢN TRÙNG, nên Add lại id cũ
        /// sẽ để lại hai vector cùng nhãn trong index và từ điển chỉ trỏ tới một trong hai.
        /// </para>
        /// <para>
        /// Việc ghi đè tồn tại để giữ đúng ngữ nghĩa của provider Redis (khóa document tất định
        /// theo phân vùng + câu hỏi): không có nó thì mỗi lần trượt sát ngưỡng lại đẻ thêm một
        /// vector gần trùng, tích thành hàng nghìn bản sao làm chậm KNN mà không tăng recall.
        /// </para>
        /// </summary>
        private void AddEntry(Partition partition, CacheEntry entry)
        {
            if (partition.IdsByQuestionHash.TryGetValue(entry.QuestionHash, out var oldId))
            {
                partition.Index.RemoveIds(new IDSelectorBatch(new[] { oldId }));
                partition.Entries.Remove(oldId);
                _entryCount--;
            }

            var id = ++_nextId;

            partition.Index.Add(1, entry.Vector, new[] { id });
            partition.Entries[id] = entry;
            partition.IdsByQuestionHash[entry.QuestionHash] = id;
            _entryCount++;
        }

        /// <summary>
        /// Ba tấm lọc chống nhiễm bẩn cache, tất cả đều cấu hình được.
        /// Giữ nguyên hệt bản Redis: hai provider phải từ chối ghi đúng cùng một tập câu, không thì
        /// bài đối chiếu giữa chúng mất ý nghĩa.
        /// </summary>
        private bool IsWorthCaching(SemanticAnswerQuery query, string answer, bool hasContext)
        {
            if (query.QuestionVector.Length == 0 || string.IsNullOrWhiteSpace(answer))
                return false;

            // Câu cực ngắn ("ừ", "thế à") nhúng ra vector nhiễu, gần như thứ gì cũng vượt ngưỡng.
            if (query.Question.Trim().Length < _shared.MinCacheableQuestionLength)
                return false;

            // Truy hồi rỗng thì LLM gần như chắc chắn trả "tôi không biết". Ghi lại là đóng băng
            // một lần Qdrant hụt thành câu trả lời chính thức cho cả một chùm câu hỏi.
            return hasContext || _shared.CacheAnswersWithoutContext;
        }

        // ---------------------------------------------------------------------------------------
        // Vận hành
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Xoá cả phân vùng.
        /// <para>
        /// Dispose nguyên index thay vì RemoveIds từng id: xoá cả phân vùng thì không còn khái niệm
        /// "id còn sót", và bộ nhớ native được trả lại ngay thay vì đợi nhịp quét sau.
        /// </para>
        /// <para>
        /// ⚠️ CỬA SỔ RỦI RO: cache ghi xuống đĩa theo kiểu write-behind. Xoá xong mà tiến trình bị
        /// SIGKILL (docker kill, hoặc quá thời gian chờ của docker stop) TRƯỚC nhịp flush kế tiếp
        /// thì câu trả lời vừa xoá SỐNG LẠI từ file ở lần khởi động sau. Tắt êm thì StopAsync có
        /// flush nên không sao. Tăng <c>ChangeCount</c> ở đây là để nhịp flush kế tiếp chắc chắn
        /// ghi; muốn cửa sổ hẹp hơn thì hạ <c>SemanticAnswerCache:Faiss:FlushIntervalSeconds</c>.
        /// </para>
        /// </summary>
        public Task<long> PurgeAsync(string npcName, string npcPersona, CancellationToken cancellationToken = default)
        {
            try
            {
                var tag = AnswerCachePartition.Tag(npcName, npcPersona);

                _lock.EnterWriteLock();
                try
                {
                    if (!_partitions.Remove(tag, out var partition))
                        return Task.FromResult(0L);

                    var removed = partition.Entries.Count;
                    _entryCount -= removed;
                    partition.Dispose();

                    Interlocked.Increment(ref _changeCount);

                    _logger.LogInformation("Đã xoá {Count} entry cache ngữ nghĩa của NPC {Npc}.", removed, npcName);

                    return Task.FromResult((long)removed);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            catch (Exception ex)
            {
                // Khác hai method của ISemanticAnswerCache: đây là đường vận hành, người gọi là con
                // người và cần biết là lệnh xoá KHÔNG chạy được. Vẫn không ném, nhưng trả 0 kèm log
                // ở mức lỗi.
                Interlocked.Increment(ref _errors);
                _logger.LogError(ex, "Không xoá được cache ngữ nghĩa của NPC {Npc}.", npcName);
                return Task.FromResult(0L);
            }
        }

        // ---------------------------------------------------------------------------------------
        // Bền vững
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Chụp lại cache để ghi đĩa. Chỉ giữ khóa ĐỌC và chỉ sao chép danh sách ra ngoài — phần
        /// chạm đĩa nằm ở <see cref="IAnswerCacheStore.SaveAsync"/>, chạy sau khi đã nhả khóa.
        /// Giữ khóa suốt 15 MB I/O là chặn toàn bộ đường ghi vài chục mili-giây mỗi nhịp flush:
        /// không hỏng, nhưng không cần.
        /// <para>
        /// Chia sẻ thẳng tham chiếu <c>float[]</c> là an toàn vì vector không bao giờ bị sửa sau khi
        /// entry được tạo; chỉ hai field thời gian là thay đổi, và chúng được đọc bằng Interlocked.
        /// </para>
        /// </summary>
        public AnswerCacheSnapshot ExportSnapshot(int maxEntries)
        {
            _lock.EnterReadLock();
            try
            {
                var entries = _partitions.Values
                    .SelectMany(partition => partition.Entries.Values)
                    .OrderByDescending(entry => Interlocked.Read(ref entry.LastAccessUtcTicks))
                    .Take(Math.Max(0, maxEntries))
                    .Select(entry => new StoredAnswer(
                        entry.Tag,
                        entry.QuestionHash,
                        entry.Question,
                        entry.Answer,
                        entry.Vector,
                        new DateTime(Interlocked.Read(ref entry.ExpiresAtUtcTicks), DateTimeKind.Utc),
                        new DateTime(Interlocked.Read(ref entry.LastAccessUtcTicks), DateTimeKind.Utc)))
                    .ToList();

                return new AnswerCacheSnapshot(entries, _dimensions);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Nạp ảnh chụp từ đĩa. TRỘN chứ không thay thế.
        /// <para>
        /// Phải trộn vì service nạp chạy <c>await Task.Yield()</c> rồi mới chạm đĩa, nên host đã bắt
        /// đầu nhận request trước khi nạp xong: một entry đang sống trong RAM là MỚI HƠN entry cùng
        /// khóa trên đĩa, và ghi đè nó là làm mất một câu trả lời vừa sinh.
        /// </para>
        /// <para>
        /// Nạp THEO LÔ — một lần <c>Add</c> cho mỗi phân vùng thay vì một lần cho mỗi entry. Với
        /// 5.000 entry đây là khác biệt giữa vài chục mili-giây và vài trăm, vì mỗi lần gọi là một
        /// lần vượt biên giới sang native.
        /// </para>
        /// </summary>
        public int ImportSnapshot(AnswerCacheSnapshot snapshot)
        {
            if (snapshot.Dimensions != _dimensions)
            {
                // Vân tay lẽ ra đã chặn từ tầng store; đây là lưới thứ hai cho trường hợp file được
                // dựng bằng tay hoặc định dạng đổi mà vân tay quên đổi theo.
                _logger.LogWarning("Bỏ qua ảnh chụp cache câu trả lời: {Found} chiều, đang chờ {Expected} chiều.",
                    snapshot.Dimensions, _dimensions);
                return 0;
            }

            var nowTicks = DateTime.UtcNow.Ticks;
            var imported = 0;

            _lock.EnterWriteLock();
            try
            {
                foreach (var group in snapshot.Entries.GroupBy(entry => entry.Tag, StringComparer.Ordinal))
                {
                    if (!_partitions.TryGetValue(group.Key, out var partition))
                    {
                        if (_partitions.Count >= _config.MaxPartitions)
                            continue;

                        partition = Partition.Create(_dimensions);
                        _partitions[group.Key] = partition;
                    }

                    var fresh = group
                        .Where(entry => entry.ExpiresAtUtc.Ticks > nowTicks)
                        .Where(entry => entry.Vector.Length == _dimensions)
                        .Where(entry => !partition.IdsByQuestionHash.ContainsKey(entry.QuestionHash))
                        .ToList();

                    if (fresh.Count == 0)
                        continue;

                    imported += AddBatch(partition, fresh);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return imported;
        }

        /// <summary>Nạp một lô entry vào một phân vùng bằng đúng một lần vượt biên sang native.</summary>
        private int AddBatch(Partition partition, IReadOnlyList<StoredAnswer> entries)
        {
            var vectors = new float[entries.Count * _dimensions];
            var ids = new long[entries.Count];

            for (var i = 0; i < entries.Count; i++)
            {
                entries[i].Vector.CopyTo(vectors, i * _dimensions);
                ids[i] = ++_nextId;
            }

            partition.Index.Add(entries.Count, vectors, ids);

            for (var i = 0; i < entries.Count; i++)
            {
                var source = entries[i];

                partition.Entries[ids[i]] = new CacheEntry
                {
                    Tag = source.Tag,
                    QuestionHash = source.QuestionHash,
                    Question = source.Question,
                    Answer = source.Answer,
                    Vector = source.Vector,
                    ExpiresAtUtcTicks = source.ExpiresAtUtc.Ticks,
                    LastAccessUtcTicks = source.LastAccessUtc.Ticks
                };

                partition.IdsByQuestionHash[source.QuestionHash] = ids[i];
                _entryCount++;
            }

            return entries.Count;
        }

        /// <summary>
        /// Xoá entry hết hạn rồi ép trần <c>MaxEntries</c>.
        /// <para>
        /// Chạy ở nhịp NỀN chứ không trên đường nóng: <c>RemoveIds</c> cần khóa ghi độc quyền, nên
        /// làm ngay lúc đọc nghĩa là mỗi lượt tra vấp phải một entry hết hạn sẽ chặn toàn bộ request
        /// khác. Dồn vào một nhịp là đổi "chặn thường xuyên, không đoán trước" lấy "chặn một lần
        /// mỗi FlushIntervalSeconds".
        /// </para>
        /// <para>
        /// Trần <c>MaxEntries</c> ép ở đây là bản thay thế cho <c>volatile-lru</c> của Redis. Loại
        /// theo <c>LastAccessUtcTicks</c> cũ nhất — LRU xấp xỉ, đủ tốt cho một cache và không cần
        /// một cấu trúc dữ liệu thứ hai để duy trì.
        /// </para>
        /// </summary>
        public int SweepExpired(DateTime utcNow)
        {
            var nowTicks = utcNow.Ticks;

            _lock.EnterWriteLock();
            try
            {
                var removed = 0;

                foreach (var partition in _partitions.Values)
                {
                    // ToList() trước khi xoá, cùng lý do với EnforceMaxEntries bên dưới.
                    var expired = partition.Entries
                        .Where(pair => Interlocked.Read(ref pair.Value.ExpiresAtUtcTicks) <= nowTicks)
                        .Select(pair => pair.Key)
                        .ToList();

                    removed += RemoveFrom(partition, expired);
                }

                removed += EnforceMaxEntries();

                // Phân vùng rỗng vẫn giữ một index native; dọn luôn để trần MaxPartitions không bị
                // ăn mòn bởi những persona đã chết.
                foreach (var tag in _partitions.Where(pair => pair.Value.Entries.Count == 0)
                                               .Select(pair => pair.Key).ToList())
                {
                    _partitions[tag].Dispose();
                    _partitions.Remove(tag);
                }

                if (removed > 0)
                    Interlocked.Increment(ref _changeCount);

                return removed;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>Gọi viên PHẢI đang giữ khóa ghi.</summary>
        private int EnforceMaxEntries()
        {
            if (_entryCount <= _config.MaxEntries)
                return 0;

            var excess = _entryCount - _config.MaxEntries;

            // ToList() BẮT BUỘC, không phải cho gọn: chuỗi LINQ ở trên đọc lười từ chính
            // partition.Entries, mà RemoveFrom bên dưới lại sửa đúng từ điển đó. Không vật chất
            // hóa trước thì đây là "sửa collection trong lúc duyệt" — một InvalidOperationException
            // chỉ nổ khi cache đã chạm trần, tức là muộn hơn hẳn mọi lần chạy thử.
            var victims = _partitions.Values
                .SelectMany(partition => partition.Entries.Select(pair => (Partition: partition, Id: pair.Key, Entry: pair.Value)))
                .OrderBy(item => Interlocked.Read(ref item.Entry.LastAccessUtcTicks))
                .Take(excess)
                .GroupBy(item => item.Partition)
                .Select(group => (Partition: group.Key, Ids: group.Select(item => item.Id).ToList()))
                .ToList();

            var removed = victims.Sum(victim => RemoveFrom(victim.Partition, victim.Ids));

            _logger.LogInformation("Cache câu trả lời chạm trần {Max} entry, đã loại {Count} entry lâu không dùng nhất.",
                _config.MaxEntries, removed);

            return removed;
        }

        /// <summary>Gọi viên PHẢI đang giữ khóa ghi. Một lần vượt biên sang native cho cả lô.</summary>
        private int RemoveFrom(Partition partition, IReadOnlyList<long> ids)
        {
            if (ids.Count == 0)
                return 0;

            partition.Index.RemoveIds(new IDSelectorBatch(ids.ToArray()));

            foreach (var id in ids)
            {
                if (!partition.Entries.Remove(id, out var entry))
                    continue;

                // Chỉ gỡ ánh xạ khi nó còn trỏ về ĐÚNG id này: một câu hỏi được ghi đè sẽ có id mới
                // trong ánh xạ, và gỡ theo câu hỏi sẽ xoá nhầm entry mới bằng id cũ.
                if (partition.IdsByQuestionHash.TryGetValue(entry.QuestionHash, out var mapped) && mapped == id)
                    partition.IdsByQuestionHash.Remove(entry.QuestionHash);

                _entryCount--;
            }

            return ids.Count;
        }

        // ---------------------------------------------------------------------------------------
        // Lỗi
        // ---------------------------------------------------------------------------------------

        private void RecordFailure(Exception exception, string operation, string subject)
        {
            Interlocked.Increment(ref _errors);

            _logger.LogWarning(exception,
                "Cache ngữ nghĩa không dùng được khi {Operation} ({Subject}); đi tiếp đường Qdrant + LLM.",
                operation, subject);
        }

        private void RecordDimensionMismatch(int found)
        {
            Interlocked.Increment(ref _errors);

            _logger.LogWarning(
                "Vector câu hỏi có {Found} chiều nhưng cache đang chờ {Expected} chiều; bỏ qua lượt cache này. " +
                "Kiểm tra EmbeddingModel:OutputDimensions.",
                found, _dimensions);
        }

        private void RecordPartitionLimitReached(string npcName)
        {
            Interlocked.Increment(ref _errors);

            // Một cảnh báo là đủ: chạm trần thì MỌI request của mọi persona mới đều rơi vào đây, và
            // lặp lại dòng này mỗi request chỉ làm chìm mất nó.
            if (Interlocked.Exchange(ref _partitionLimitWarned, 1) != 0)
                return;

            _logger.LogWarning(
                "Cache ngữ nghĩa đã chạm trần {Max} phân vùng, không ghi thêm cho NPC {Npc}. " +
                "Mỗi cặp (tên NPC, mô tả tính cách) là một phân vùng, mà mô tả tính cách do client gửi lên " +
                "theo từng request — hãy kiểm tra client có đang đổi nó mỗi lần hay không trước khi nâng " +
                "SemanticAnswerCache:Faiss:MaxPartitions.",
                _config.MaxPartitions, npcName);
        }

        public void Dispose()
        {
            // Index ôm bộ nhớ NATIVE, thứ GC hoàn toàn không nhìn thấy: quên dispose ở đây là rò bộ
            // nhớ mà không một công cụ .NET nào báo.
            foreach (var partition in _partitions.Values)
                partition.Dispose();

            _partitions.Clear();
            _lock.Dispose();
        }

        // ---------------------------------------------------------------------------------------
        // Trạng thái nội bộ
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Một phân vùng — tức một cặp (tên NPC, mô tả tính cách) — có index FAISS RIÊNG.
        /// <para>
        /// Phân vùng là thuộc tính BẢO MẬT chứ không phải tối ưu, nên nó phải đúng theo CẤU TRÚC.
        /// Provider Redis đạt điều đó bằng bộ lọc TRƯỚC ("(@npc:{tag})=&gt;[KNN ...]"): document của
        /// NPC khác không thể lọt vào tập ứng viên. FAISS không có bộ lọc metadata, nên cách duy
        /// nhất tương đương là index riêng — một truy vấn về mặt vật lý không chạm được vector của
        /// phân vùng khác, và không có dòng "nếu quên kiểm tra tag" nào để mà quên.
        /// </para>
        /// <para>
        /// Phương án một index chung kèm lọc SAU đã bị loại: láng giềng gần nhất toàn cục hoàn toàn
        /// có thể thuộc NPC khác, lọc xong còn rỗng, thành một lần TRƯỢT OAN hoàn toàn vô hình mà
        /// vẫn tốn trọn một lượt gọi LLM. Vá bằng cách nâng k lên 20-50 là đoán mò, vì k đúng phụ
        /// thuộc phân bố số entry giữa các NPC — thứ thay đổi liên tục lúc chạy.
        /// </para>
        /// <para>
        /// Chi phí gần bằng 0: FLAT lưu vector thô liên tục nên tổng bộ nhớ của N index nhỏ đúng
        /// bằng một index lớn, và một phân vùng rỗng chỉ là vài trăm byte.
        /// </para>
        /// </summary>
        private sealed class Partition : IDisposable
        {
            public required IndexIDMap2<IndexFlatIP> Index { get; init; }

            public Dictionary<long, CacheEntry> Entries { get; } = new();

            /// <summary>
            /// Ánh xạ câu hỏi → id đang giữ nó. Tồn tại để đường ghi tìm được entry cũ cần ghi đè.
            /// <para>
            /// Đây cũng là lý do KHÔNG dùng hash 64-bit của khóa làm id FAISS: từ điển này dù sao
            /// cũng phải có, nên hash-id không tiết kiệm được gì mà chỉ thêm một khả năng va chạm,
            /// và hậu quả của va chạm ở đây là hai câu hỏi khác nhau dùng chung một slot — một câu
            /// bị ghi đè bởi câu kia, tức là NPC trả lời bằng nội dung của câu hỏi khác, không thể
            /// phát hiện được.
            /// </para>
            /// </summary>
            public Dictionary<string, long> IdsByQuestionHash { get; } = new(StringComparer.Ordinal);

            /// <summary>
            /// <c>IndexFlatIP</c> bọc trong <c>IndexIDMap2</c> để dùng được id tự đặt.
            /// IDMap2 chứ không IDMap: nó giữ thêm ánh xạ ngược nên hỗ trợ <c>Reconstruct</c> theo
            /// id — đường cắt một nửa bộ nhớ sau này, nếu bỏ được bản sao vector trong metadata.
            /// <c>takeOwnership</c> để index con được dispose cùng lớp bọc, không thì rò bộ nhớ native.
            /// </summary>
            public static Partition Create(int dimensions) =>
                new() { Index = new IndexIDMap2<IndexFlatIP>(new IndexFlatIP(dimensions), takeOwnership: true) };

            public void Dispose() => Index.Dispose();
        }

        /// <summary>
        /// CLASS chứ không phải record, và đó là chủ ý: hai field thời gian được cập nhật bằng
        /// <see cref="Interlocked"/> ngay trên đường ĐỌC (sliding TTL). Nếu entry bất biến thì mỗi
        /// lần TRÚNG cache đều phải nâng lên khóa ghi độc quyền để thay entry trong từ điển — biến
        /// đường nóng thành nút cổ chai vì đúng cái việc rẻ nhất trong cả lớp.
        /// </summary>
        private sealed class CacheEntry
        {
            public required string Tag { get; init; }
            public required string QuestionHash { get; init; }
            public required string Question { get; init; }
            public required string Answer { get; init; }

            /// <summary>ĐÃ chuẩn hóa L2. Không bao giờ bị sửa sau khi entry được tạo.</summary>
            public required float[] Vector { get; init; }

            /// <summary>Field chứ không property: <see cref="Interlocked"/> cần tham chiếu tới ô nhớ.</summary>
            public long ExpiresAtUtcTicks;

            public long LastAccessUtcTicks;
        }
    }
}
