using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Interface;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Cắt văn bản theo kích thước cố định nhưng co lại tới dấu kết câu gần nhất, để đoạn không
    /// bị đứt giữa câu. Thuật toán giữ nguyên như bản static trước đây; chỉ khác là mọi hằng số
    /// đã ra <see cref="ChunkingConfig"/>.
    /// </summary>
    public sealed class SentenceAwareChunker : IChunkingStrategy
    {
        private readonly ChunkingConfig _chunking;
        private readonly RagConfig _rag;
        private readonly char[] _sentenceEndings;

        public SentenceAwareChunker(IOptions<ChunkingConfig> chunking, IOptions<RagConfig> rag)
        {
            _chunking = chunking.Value;
            _rag = rag.Value;
            _sentenceEndings = _chunking.SentenceEndings.ToCharArray();
        }

        public IEnumerable<string> Chunk(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            var chunkSize = Math.Max(1, _rag.ChunkSize);
            var overlap = Math.Max(0, _rag.ChunkOverlap);

            var start = 0;

            while (start < text.Length)
            {
                var end = Math.Min(start + chunkSize, text.Length);

                // Chỉ cố cắt theo dấu câu khi chưa chạm đáy văn bản.
                if (end < text.Length)
                    end = ShrinkToSentenceEnd(text, start, end, chunkSize);

                var chunk = text[start..end].Trim();

                if (!string.IsNullOrEmpty(chunk))
                    yield return chunk;

                // Overlap lớn hơn đoạn vừa cắt sẽ khiến con trỏ đứng yên hoặc lùi lại — tức là
                // vòng lặp vô tận. Ép tiến tới cuối đoạn hiện tại trong trường hợp đó.
                var nextStart = end - overlap;
                start = nextStart <= start ? end : nextStart;
            }
        }

        /// <summary>
        /// Lùi <paramref name="end"/> về dấu kết câu gần nhất trong khoảng nhìn lại, miễn là đoạn
        /// còn lại vẫn đủ dài. Không tìm được thì giữ nguyên vị trí cắt cũ.
        /// </summary>
        private int ShrinkToSentenceEnd(string text, int start, int end, int chunkSize)
        {
            var lookback = Math.Min(
                Math.Max(_chunking.MinLookback, (int)(chunkSize * _chunking.LookbackRatio)),
                end - start);

            var lastEnding = text.LastIndexOfAny(_sentenceEndings, end - 1, lookback);

            var minimumLength = (int)(chunkSize * _chunking.MinChunkRatio);

            return lastEnding != -1 && lastEnding > start + minimumLength
                ? lastEnding + 1
                : end;
        }
    }
}
