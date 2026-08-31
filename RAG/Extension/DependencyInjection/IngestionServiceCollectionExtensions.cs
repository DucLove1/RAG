using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;
using RAG.Class.Ingestion;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký đường nạp tri thức.</summary>
    public static class IngestionServiceCollectionExtensions
    {
        /// <summary>
        /// Bind một options class rồi VALIDATE NGAY LÚC KHỞI ĐỘNG.
        /// <para>
        /// Không có bước này thì thiếu một biến môi trường vẫn khởi động bình thường, rồi chết ở
        /// request đầu tiên bằng một <c>UriFormatException</c> không nói lên điều gì. Fail-fast lúc
        /// khởi động biến lỗi cấu hình thành lỗi triển khai thay vì lỗi lúc chạy.
        /// </para>
        /// </summary>
        /// <summary>
        /// Đăng ký đường nạp tri thức: bộ đọc theo định dạng, chiến lược cắt đoạn và service nạp.
        /// <para>
        /// Bộ đọc đăng ký dạng nhiều cài đặt cho cùng một interface: thêm định dạng mới chỉ là thêm
        /// một lớp và một dòng ở đây, không sửa service lẫn controller (OCP).
        /// </para>
        /// </summary>
        public static IServiceCollection AddIngestion(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddValidatedOptions<IngestionConfig>(configuration, IngestionConfig.SectionName);
            services.AddValidatedOptions<ChunkingConfig>(configuration, ChunkingConfig.SectionName);

            services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
            services.AddSingleton<IChunkingStrategy, SentenceAwareChunker>();
            services.AddSingleton<IIngestionService, DocumentIngestionService>();

            return services;
        }
    }
}
