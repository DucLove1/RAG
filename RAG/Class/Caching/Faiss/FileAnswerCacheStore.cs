using System.Text;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;

namespace RAG.Class.Caching.Faiss
{
    /// <summary>
    /// Lưu cache câu trả lời ngữ nghĩa ra một file nhị phân.
    /// <para>
    /// Cố ý KHÔNG dùng <c>IndexSerializer</c> của FAISS, dù nó có sẵn. Bốn lý do, xếp theo sức nặng:
    /// </para>
    /// <para>
    /// 1. HAI FILE THÌ KHÔNG NGUYÊN TỬ ĐƯỢC. <see cref="AtomicFileWriter"/> đảm bảo nguyên tử cho
    /// MỘT file, không phải cho một cặp. Tiến trình chết giữa hai lần đổi tên sẽ để lại index và
    /// metadata LỆCH PHA — và hậu quả không phải là crash mà là một câu hỏi khớp vào metadata của
    /// câu khác: im lặng, không log, không exception.
    /// </para>
    /// <para>
    /// 2. ĐỊNH DẠNG CỦA FAISS KHÔNG THUỘC QUYỀN TA. Binding đang ở trạng thái alpha; định dạng
    /// serialize đổi giữa hai bản preview là chuyện bình thường, và vân tay ở đây không bắt được
    /// chuyện đó vì nó nằm bên trong file của FAISS.
    /// </para>
    /// <para>
    /// 3. FLAT KHÔNG CÓ GÌ ĐÁNG SERIALIZE. Không training, không đồ thị — dựng lại chỉ là chép N
    /// vector, làm theo lô thì vài chục mili-giây cho 5.000 entry. (Nếu một ngày đổi sang HNSW thì
    /// bài toán này lật ngược: dựng lại đồ thị HNSW đắt thật, và lúc đó IndexSerializer mới đáng
    /// giá.)
    /// </para>
    /// <para>
    /// 4. GIỮ ĐƯỢC LUẬT CÔ LẬP. Dùng IndexSerializer thì chính file này cũng phải import
    /// <c>Faiss.*</c>, và FAISS rò ra thành hai file thay vì một.
    /// </para>
    /// <para>
    /// Ghi nhị phân chứ không JSON, cùng lý do đã đo ở <see cref="FileQueryCacheStore"/>: mỗi vector
    /// 768 chiều tốn 9.591 byte dưới dạng JSON so với 3.072 byte nhị phân. File này được ghi lại
    /// định kỳ suốt vòng đời ứng dụng nên chênh lệch đó là đáng kể.
    /// </para>
    /// <para>
    /// Cache là tối ưu hóa, không phải nguồn sự thật: MỌI lỗi đọc/ghi đều được nuốt và chỉ ghi log.
    /// </para>
    /// </summary>
    public sealed class FileAnswerCacheStore : IAnswerCacheStore
    {
        /// <summary>Nhận diện định dạng. Đổi cấu trúc file thì tăng số cuối để file cũ bị bỏ qua.</summary>
        private const string Magic = "RAGAC1";

        private readonly SemanticAnswerCacheFaissConfig _config;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<FileAnswerCacheStore> _logger;

        public FileAnswerCacheStore(IOptions<SemanticAnswerCacheFaissConfig> options,
                                    IHostEnvironment environment,
                                    ILogger<FileAnswerCacheStore> logger)
        {
            _config = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public Task<AnswerCacheSnapshot?> LoadAsync(string fingerprint, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath();

            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogDebug("Chưa có file cache câu trả lời tại {Path}.", path);
                    return Task.FromResult<AnswerCacheSnapshot?>(null);
                }

                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                if (reader.ReadString() != Magic)
                {
                    _logger.LogWarning("File cache câu trả lời sai định dạng, bỏ qua.");
                    return Task.FromResult<AnswerCacheSnapshot?>(null);
                }

                if (!string.Equals(reader.ReadString(), fingerprint, StringComparison.Ordinal))
                {
                    // Đổi model nhúng hay số chiều thì vector cũ không còn cùng không gian với
                    // vector mới. Đây là chỗ FAISS tốt hơn Redis: ở đó quên bump IndexName là im
                    // lặng trả câu trả lời dựng trên dữ liệu cũ, còn ở đây file tự bị loại.
                    _logger.LogInformation("Vân tay cache câu trả lời đã cũ (model hoặc số chiều đã đổi), bỏ qua.");
                    return Task.FromResult<AnswerCacheSnapshot?>(null);
                }

                var count = reader.ReadInt32();
                var dimensions = reader.ReadInt32();
                var entries = new List<StoredAnswer>(Math.Max(0, count));

                for (var i = 0; i < count; i++)
                    entries.Add(ReadEntry(reader, dimensions));

                _logger.LogInformation("Đã nạp cache câu trả lời: {Count} entry, {Dimensions} chiều từ {Path}.",
                    entries.Count, dimensions, path);

                return Task.FromResult<AnswerCacheSnapshot?>(new AnswerCacheSnapshot(entries, dimensions));
            }
            catch (Exception ex)
            {
                // Bắt cả EndOfStreamException của file cụt: container bị SIGKILL giữa lúc ghi là
                // chuyện thường, và một file cụt không phải lý do để app không khởi động được.
                _logger.LogWarning(ex, "Không đọc được cache câu trả lời tại {Path}, coi như chưa có.", path);
                return Task.FromResult<AnswerCacheSnapshot?>(null);
            }
        }

        private static StoredAnswer ReadEntry(BinaryReader reader, int dimensions)
        {
            var tag = reader.ReadString();
            var questionHash = reader.ReadString();
            var question = reader.ReadString();
            var answer = reader.ReadString();
            var expiresAt = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
            var lastAccess = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);

            var vector = new float[dimensions];
            for (var i = 0; i < dimensions; i++)
                vector[i] = reader.ReadSingle();

            return new StoredAnswer(tag, questionHash, question, answer, vector, expiresAt, lastAccess);
        }

        public Task<bool> SaveAsync(string fingerprint, AnswerCacheSnapshot snapshot,
                                    CancellationToken cancellationToken = default)
        {
            var path = ResolvePath();

            try
            {
                // Mọi vector phải cùng số chiều thì mới ghi được số chiều một lần ở đầu khối.
                // Vector lệch chiều là dấu hiệu dữ liệu hỏng nên bị loại luôn, không ghi xuống đĩa.
                var entries = snapshot.Entries.Where(entry => entry.Vector.Length == snapshot.Dimensions).ToList();

                if (entries.Count != snapshot.Entries.Count)
                {
                    _logger.LogWarning("Bỏ {Count} entry lệch số chiều khi ghi cache câu trả lời.",
                        snapshot.Entries.Count - entries.Count);
                }

                AtomicFileWriter.Write(path, stream =>
                {
                    using var writer = new BinaryWriter(stream, Encoding.UTF8);

                    writer.Write(Magic);
                    writer.Write(fingerprint);
                    writer.Write(entries.Count);
                    writer.Write(snapshot.Dimensions);

                    foreach (var entry in entries)
                    {
                        writer.Write(entry.Tag);
                        writer.Write(entry.QuestionHash);
                        writer.Write(entry.Question);
                        writer.Write(entry.Answer);
                        writer.Write(entry.ExpiresAtUtc.Ticks);
                        writer.Write(entry.LastAccessUtc.Ticks);

                        foreach (var value in entry.Vector)
                            writer.Write(value);
                    }
                });

                _logger.LogInformation("Đã ghi cache câu trả lời: {Count} entry vào {Path}.", entries.Count, path);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không ghi được cache câu trả lời vào {Path}.", path);
                return Task.FromResult(false);
            }
        }

        private string ResolvePath() => AppDataPath.Resolve(_environment, _config.PersistPath);
    }
}
