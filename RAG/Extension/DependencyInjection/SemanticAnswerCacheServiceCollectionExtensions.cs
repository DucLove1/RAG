using System.Text;
using RAG.Class.Caching;
using RAG.Class.Caching.Faiss;
using RAG.Class.Caching.Redis;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Extension.DependencyInjection
{
    /// <summary>Đăng ký tầng cache câu trả lời theo ngữ nghĩa, theo provider được chọn trong cấu hình.</summary>
    public static class SemanticAnswerCacheServiceCollectionExtensions
    {
        public static IServiceCollection AddSemanticAnswerCache(this IServiceCollection services,
                                                                IConfiguration configuration)
        {
            var section = configuration.GetSection(SemanticAnswerCacheConfig.SectionName);

            ThrowIfObsoleteKeysPresent(section);

            services.AddValidatedOptions<SemanticAnswerCacheConfig>(configuration, SemanticAnswerCacheConfig.SectionName);

            var config = section.Get<SemanticAnswerCacheConfig>() ?? new SemanticAnswerCacheConfig();

            if (!config.Enabled)
                return services.AddNullAnswerCache();

            // CHỈ đăng ký phần phụ thuộc của provider đang chọn, cùng nguyên tắc với
            // AddSemanticRouter. Đăng ký sẵn cả hai để "đổi cho nhanh" là một cái bẫy có ba mặt:
            //
            //  1. Options của Redis sẽ bị ValidateOnStart đánh giá, và [Required] ConnectionString
            //     làm app KHÔNG KHỞI ĐỘNG NỔI trong khi đang chạy FAISS.
            //  2. AddHostedService KHÔNG keyed được, nên service ghi đĩa của FAISS vẫn chạy và ghi
            //     một cache rỗng đè lên file mỗi nhịp flush khi đang dùng Redis.
            //  3. RedisConnectionProvider là singleton IDisposable, bị container dựng dù không ai gọi.
            //
            // Đó cũng là lý do đây là switch chứ không phải Keyed Services: khác ILLMProvider (nơi
            // hai provider SỐNG CÙNG LÚC — router dùng Gemini trong khi đường trả lời dùng Groq),
            // ở đây chỉ đúng MỘT provider tồn tại. Xem AnswerCacheProvider.
            return config.Provider switch
            {
                AnswerCacheProvider.Faiss => services.AddFaissAnswerCache(configuration),
                _ => services.AddRedisAnswerCache(configuration)
            };
        }

        /// <summary>
        /// Null Object thay vì rải if trong AskPipeline: đường trả lời không cần biết tầng cache
        /// này có tồn tại hay không.
        /// </summary>
        private static IServiceCollection AddNullAnswerCache(this IServiceCollection services)
        {
            services.AddSingleton<NullSemanticAnswerCache>();
            services.AddSingleton<ISemanticAnswerCache>(sp => sp.GetRequiredService<NullSemanticAnswerCache>());
            services.AddSingleton<ISemanticAnswerCacheStatistics>(sp => sp.GetRequiredService<NullSemanticAnswerCache>());
            services.AddSingleton<ISemanticAnswerCacheAdmin>(sp => sp.GetRequiredService<NullSemanticAnswerCache>());

            return services;
        }

        private static IServiceCollection AddRedisAnswerCache(this IServiceCollection services,
                                                              IConfiguration configuration)
        {
            services.AddValidatedOptions<SemanticAnswerCacheRedisConfig>(
                configuration, SemanticAnswerCacheRedisConfig.SectionName);

            services.AddSingleton<IRedisConnection, RedisConnectionProvider>();

            // Đăng ký lớp cụ thể MỘT lần rồi trỏ từng interface về đúng instance đó. Đăng ký rời
            // từng interface (AddSingleton<ISemanticAnswerCache, RedisSemanticAnswerCache>() và
            // tương tự) sẽ dựng BA instance độc lập: ba bộ đếm số liệu rời nhau, ba cờ _indexEnsured
            // rời nhau, và endpoint cache-stats sẽ báo toàn số 0 trong khi cache vẫn đang chạy.
            services.AddSingleton<RedisSemanticAnswerCache>();
            services.AddSingleton<ISemanticAnswerCache>(sp => sp.GetRequiredService<RedisSemanticAnswerCache>());
            services.AddSingleton<ISemanticAnswerCacheStatistics>(sp => sp.GetRequiredService<RedisSemanticAnswerCache>());
            services.AddSingleton<ISemanticAnswerCacheAdmin>(sp => sp.GetRequiredService<RedisSemanticAnswerCache>());

            return services;
        }

        private static IServiceCollection AddFaissAnswerCache(this IServiceCollection services,
                                                              IConfiguration configuration)
        {
            services.AddValidatedOptions<SemanticAnswerCacheFaissConfig>(
                configuration, SemanticAnswerCacheFaissConfig.SectionName);

            // Cùng lý do với nhánh Redis, và ở đây còn nặng hơn: đăng ký rời sẽ dựng BỐN instance,
            // mỗi instance một bộ index FAISS riêng — service flush sẽ ghi xuống đĩa đúng cái cache
            // mà pipeline không hề dùng, và bộ nhớ native nhân lên bốn lần.
            services.AddSingleton<FaissSemanticAnswerCache>();
            services.AddSingleton<ISemanticAnswerCache>(sp => sp.GetRequiredService<FaissSemanticAnswerCache>());
            services.AddSingleton<ISemanticAnswerCacheStatistics>(sp => sp.GetRequiredService<FaissSemanticAnswerCache>());
            services.AddSingleton<ISemanticAnswerCacheAdmin>(sp => sp.GetRequiredService<FaissSemanticAnswerCache>());
            services.AddSingleton<IPersistableAnswerCache>(sp => sp.GetRequiredService<FaissSemanticAnswerCache>());

            var faiss = configuration.GetSection(SemanticAnswerCacheFaissConfig.SectionName)
                                     .Get<SemanticAnswerCacheFaissConfig>() ?? new SemanticAnswerCacheFaissConfig();

            // PersistPath rỗng = cache chỉ sống trong RAM, cùng quy ước với QueryCache:PersistPath.
            // Lúc đó không đăng ký gì thêm: không có store thì cũng không có service ghi đĩa.
            if (!string.IsNullOrWhiteSpace(faiss.PersistPath))
            {
                services.AddSingleton<IAnswerCacheStore, FileAnswerCacheStore>();
                services.AddHostedService<AnswerCachePersistenceService>();
            }

            return services;
        }

        /// <summary>
        /// Nổ ngay lúc khởi động khi cấu hình còn dùng khóa phẳng cũ đã dời vào section con.
        /// Lý do phải nổ thay vì chỉ log nằm ở <see cref="ObsoleteAnswerCacheKeys"/>.
        /// </summary>
        private static void ThrowIfObsoleteKeysPresent(IConfigurationSection section)
        {
            // Đọc khóa TRỰC TIẾP dưới section cha. "SemanticAnswerCache:Redis:ConnectionString" là
            // một khóa khác hẳn nên không có dương tính giả sau khi đã di trú.
            var present = ObsoleteAnswerCacheKeys.Moved
                .Where(entry => section[entry.Key] is not null)
                .ToList();

            if (present.Count == 0)
                return;

            var message = new StringBuilder(ObsoleteAnswerCacheKeys.MessageHeader).AppendLine().AppendLine();

            foreach (var entry in present)
            {
                message.AppendLine(string.Format(ObsoleteAnswerCacheKeys.MessageLineFormat,
                    $"{SemanticAnswerCacheConfig.SectionName}:{entry.Key}", entry.Value));
            }

            message.AppendLine().Append(ObsoleteAnswerCacheKeys.MessageFooter);

            throw new InvalidOperationException(message.ToString());
        }
    }
}
