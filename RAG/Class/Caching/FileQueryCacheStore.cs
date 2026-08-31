using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;
using System.Text;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Lưu cache hỏi đáp ra một file nhị phân.
    /// <para>
    /// Cố tình KHÔNG dùng JSON như các cache khác. Đo thực tế trên route-vectors.json: mỗi vector
    /// 768 chiều tốn 9.591 byte dưới dạng JSON (float ghi thành chữ) so với 3.072 byte nhị phân —
    /// phình hơn ba lần. Route cache chỉ ghi một lần lúc warm-up nên chấp nhận được; cache này ghi
    /// định kỳ suốt vòng đời ứng dụng nên chênh lệch đó là đáng kể.
    /// </para>
    /// <para>
    /// Cache là tối ưu hóa, không phải nguồn sự thật: MỌI lỗi đọc/ghi đều được nuốt và chỉ ghi log.
    /// </para>
    /// </summary>
    public sealed class FileQueryCacheStore : IQueryCacheStore
    {
        /// <summary>Nhận diện định dạng. Đổi cấu trúc file thì tăng số cuối để file cũ bị bỏ qua.</summary>
        private const string Magic = "RAGQC1";

        private readonly QueryCacheConfig _config;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<FileQueryCacheStore> _logger;

        public FileQueryCacheStore(IOptions<QueryCacheConfig> options,
                                   IHostEnvironment environment,
                                   ILogger<FileQueryCacheStore> logger)
        {
            _config = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public Task<QueryCacheSnapshot?> LoadAsync(string fingerprint, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath();

            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogDebug("Chưa có file cache hỏi đáp tại {Path}.", path);
                    return Task.FromResult<QueryCacheSnapshot?>(null);
                }

                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                if (reader.ReadString() != Magic)
                {
                    _logger.LogWarning("File cache hỏi đáp sai định dạng, bỏ qua.");
                    return Task.FromResult<QueryCacheSnapshot?>(null);
                }

                if (!string.Equals(reader.ReadString(), fingerprint, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Vân tay cache hỏi đáp đã cũ (model hoặc số chiều đã đổi), bỏ qua.");
                    return Task.FromResult<QueryCacheSnapshot?>(null);
                }

                var normalizations = ReadNormalizations(reader);
                var embeddings = ReadEmbeddings(reader);

                _logger.LogInformation("Đã nạp cache hỏi đáp: {Norm} chuẩn hóa, {Emb} vector từ {Path}.",
                    normalizations.Count, embeddings.Count, path);

                return Task.FromResult<QueryCacheSnapshot?>(new QueryCacheSnapshot(normalizations, embeddings));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được cache hỏi đáp tại {Path}, coi như chưa có.", path);
                return Task.FromResult<QueryCacheSnapshot?>(null);
            }
        }

        private static List<StoredNormalization> ReadNormalizations(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            var result = new List<StoredNormalization>(Math.Max(0, count));

            for (var i = 0; i < count; i++)
            {
                var question = reader.ReadString();
                var normalized = reader.ReadString();
                var unchanged = reader.ReadBoolean();
                result.Add(new StoredNormalization(question, normalized, unchanged));
            }

            return result;
        }

        private static List<StoredEmbedding> ReadEmbeddings(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            var dims = reader.ReadInt32();
            var result = new List<StoredEmbedding>(Math.Max(0, count));

            for (var i = 0; i < count; i++)
            {
                var text = reader.ReadString();
                var vector = new float[dims];

                for (var j = 0; j < dims; j++)
                    vector[j] = reader.ReadSingle();

                result.Add(new StoredEmbedding(text, vector));
            }

            return result;
        }

        public Task<bool> SaveAsync(string fingerprint, QueryCacheSnapshot snapshot,
                                    CancellationToken cancellationToken = default)
        {
            var path = ResolvePath();

            try
            {
                // Mọi vector phải cùng số chiều thì mới ghi được số chiều một lần ở đầu khối.
                // Vector lệch chiều là dấu hiệu dữ liệu hỏng nên bị loại luôn, không ghi xuống đĩa.
                var dims = snapshot.Embeddings.Count > 0 ? snapshot.Embeddings[0].Vector.Length : 0;
                var embeddings = snapshot.Embeddings.Where(e => e.Vector.Length == dims).ToList();

                if (embeddings.Count != snapshot.Embeddings.Count)
                {
                    _logger.LogWarning("Bỏ {Count} vector lệch số chiều khi ghi cache.",
                        snapshot.Embeddings.Count - embeddings.Count);
                }

                AtomicFileWriter.Write(path, stream =>
                {
                    using var writer = new BinaryWriter(stream, Encoding.UTF8);

                    writer.Write(Magic);
                    writer.Write(fingerprint);

                    writer.Write(snapshot.Normalizations.Count);
                    foreach (var entry in snapshot.Normalizations)
                    {
                        writer.Write(entry.Question);
                        writer.Write(entry.Normalized);
                        writer.Write(entry.Unchanged);
                    }

                    writer.Write(embeddings.Count);
                    writer.Write(dims);
                    foreach (var entry in embeddings)
                    {
                        writer.Write(entry.Text);
                        foreach (var value in entry.Vector)
                            writer.Write(value);
                    }
                });

                _logger.LogInformation("Đã ghi cache hỏi đáp: {Norm} chuẩn hóa, {Emb} vector vào {Path}.",
                    snapshot.Normalizations.Count, embeddings.Count, path);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không ghi được cache hỏi đáp vào {Path}.", path);
                return Task.FromResult(false);
            }
        }

        private string ResolvePath() => AppDataPath.Resolve(_environment, _config.PersistPath);
    }
}
