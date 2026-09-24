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
            services.AddSingleton<IDocumentTextExtractor, MarkdownFrontMatterExtractor>();

            // Phân quyền tri thức. Đăng ký ở đây vì nó suy ra từ front matter của corpus, tức là
            // thuộc về đường nạp — nhưng nó là NGUỒN SỰ THẬT DUY NHẤT và bộ nạp đồ thị ở Phase 3
            // sẽ dùng lại đúng instance này để sinh duoc_biet.
            services.AddSingleton<INpcNameResolver, EntityAliasNpcNameResolver>();
            services.AddSingleton<IAccessPolicy, FrontMatterAccessPolicy>();

            // switch chứ không phải Keyed Services: chỉ đúng MỘT chiến lược tồn tại tại một thời
            // điểm, cùng lý do với AddSemanticAnswerCache. Xem ChunkingStrategyKind.
            var chunking = configuration.GetSection(ChunkingConfig.SectionName)
                                        .Get<ChunkingConfig>() ?? new ChunkingConfig();

            switch (chunking.Strategy)
            {
                case ChunkingStrategyKind.Line:
                    services.AddSingleton<IChunkingStrategy, LineChunker>();
                    break;
                default:
                    services.AddSingleton<IChunkingStrategy, SentenceAwareChunker>();
                    break;
            }

            services.AddSingleton<IIngestionService, DocumentIngestionService>();
            services.AddSingleton<ICorpusIngestionService, CorpusIngestionService>();

            return services;
        }
    }
}
