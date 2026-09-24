using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Ingestion
{
    /// <summary>
    /// Mỗi dòng không trắng là một đoạn.
    /// <para>
    /// ĐÂY LÀ BẢN CHÉP CỦA MỘT CÔNG THỨC ĐÃ CÔNG BỐ, không phải một chiến lược cắt tự do. HUONG_DAN.md của
    /// kho tri thức định nghĩa mã chunk bằng đúng đoạn mã sau:
    /// <code>
    /// for i, dong in enumerate([d.strip() for d in than.split("\n") if d.strip()], start=1):
    ///     id = f'{meta["doc_id"]}#L{i}'
    /// </code>
    /// Toàn bộ dữ liệu đồ thị viết tay đều trỏ về những mã sinh ra theo công thức đó. Lệch một chút
    /// ở đây — đếm cả dòng trắng, đếm từ 0, quên bóc front matter — là mọi mã chunk trượt đi và đồ
    /// thị nối vào hư không, KHÔNG kèm một dòng lỗi nào: local search chỉ lặng lẽ trả về 0 cạnh cho
    /// 100% câu hỏi.
    /// </para>
    /// <para>
    /// Ba điểm dễ làm sai, viết ra để không ai "dọn dẹp" nhầm:
    /// dòng trắng bị LOẠI HẲN và KHÔNG tiêu thụ số thứ tự (số chạy trên dòng không trắng, không
    /// phải dòng vật lý); số bắt đầu từ 1; và <c>\n</c> ở cuối file không đẻ ra một dòng rỗng thứ
    /// n+1 vì dòng rỗng vốn đã bị loại.
    /// </para>
    /// <para>
    /// Không đọc <c>RAG:ChunkSize</c> và <c>RAG:ChunkOverlap</c>: kích thước đoạn ở đây do người
    /// viết corpus quyết định khi xuống dòng, không phải do cấu hình. Hai núm đó vẫn phục vụ
    /// <see cref="SentenceAwareChunker"/>.
    /// </para>
    /// </summary>
    public sealed class LineChunker : IChunkingStrategy
    {
        public IEnumerable<TextSegment> Chunk(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            var ordinal = ChunkCodes.FirstOrdinal;

            // Chuẩn hóa CRLF trước khi tách: file corpus có thể checkout ra với line ending nào cũng
            // được, mà số thứ tự dòng thì không được phụ thuộc vào chuyện đó.
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();

                if (line.Length == 0)
                    continue;

                yield return new TextSegment(line, ordinal);
                ordinal++;
            }
        }
    }
}
