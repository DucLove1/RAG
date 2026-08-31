using RAG.Class.Constants;
using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình kết nối và cấu trúc collection của Qdrant.
    /// <para>
    /// Cố tình KHÔNG có <c>Dimensions</c>. Trước đây trường đó tồn tại song song với
    /// <c>Gemini:OutputDimensions</c> — hai nguồn sự thật cho cùng một con số. Lệch nhau thì
    /// collection được tạo với số chiều khác số chiều vector thật, và triệu chứng duy nhất là
    /// truy hồi trả về kết quả vô nghĩa. Giờ số chiều chỉ đến từ <c>IEmbeddingProvider</c>.
    /// </para>
    /// </summary>
    public class QDrantConfig
    {
        public const string SectionName = "QDRANT";

        [Required(AllowEmptyStrings = false)]
        public string Host { get; set; } = "localhost";

        [Range(1, 65535)]
        public int Port { get; set; } = 32772;

        [Required(AllowEmptyStrings = false)]
        public string Collection { get; set; } = "basic-collection";

        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Qdrant Cloud bắt buộc TLS; instance chạy local thường thì không.</summary>
        public bool UseHttps { get; set; } = true;

        /// <summary>
        /// Độ đo khoảng cách của collection, tên theo enum <c>Qdrant.Client.Grpc.Distance</c>
        /// (Cosine, Euclid, Dot, Manhattan). Đổi giá trị này chỉ có tác dụng khi TẠO MỚI collection.
        /// </summary>
        public string Distance { get; set; } = "Cosine";

        /// <summary>
        /// Cách khớp tên NPC khi lọc kết quả truy hồi. Trước đây <c>MatchPhrase</c> nằm cứng
        /// trong pipeline, tức là đổi chiến lược lọc phải sửa code của tầng nghiệp vụ.
        /// </summary>
        public PayloadFilterMode FilterMode { get; set; } = PayloadFilterMode.Phrase;

        /// <summary>Cấu hình index toàn văn cho trường lọc theo tên NPC.</summary>
        public QdrantTextIndexConfig NpcNameIndex { get; set; } = new();
    }

    /// <summary>
    /// Tham số index toàn văn của Qdrant. Trước đây bốn giá trị này nằm cứng trong
    /// <c>CreateCollectionAsync</c>, nên muốn thử tokenizer khác là phải build lại.
    /// </summary>
    public class QdrantTextIndexConfig
    {
        /// <summary>Tên theo enum <c>Qdrant.Client.Grpc.TokenizerType</c> (Word, Whitespace, Prefix, Multilingual).</summary>
        public string Tokenizer { get; set; } = "Word";

        public bool Lowercase { get; set; } = true;

        public bool PhraseMatching { get; set; } = true;
    }
}
