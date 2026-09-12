namespace RAG.Extension.DependencyInjection
{
    /// <summary>
    /// Bọc một đăng ký đã có bằng một decorator, SAU khi nó được đăng ký.
    /// <para>
    /// Các decorator cache trong repo này được bọc ngay tại chỗ đăng ký (xem
    /// <c>EmbeddingServiceCollectionExtensions</c>): mỗi cái sửa đúng một dòng
    /// <c>AddSingleton</c> của chính module đó. Cách ấy hợp lý khi decorator gắn liền với một
    /// module, nhưng việc đo giờ thì cắt ngang TÁM interface thuộc BẢY module khác nhau — làm theo
    /// kiểu cũ nghĩa là rải sửa cả bảy file để phục vụ một mối quan tâm duy nhất.
    /// </para>
    /// <para>
    /// Có helper này thì toàn bộ việc đo giờ nằm gọn trong một file đăng ký, và bảy module kia
    /// không cần biết là chúng đang bị bọc (OCP).
    /// </para>
    /// </summary>
    public static class ServiceCollectionDecorationExtensions
    {
        /// <param name="decorator">Nhận instance gốc và <c>IServiceProvider</c>, trả về bản đã bọc.</param>
        internal static IServiceCollection Decorate<TService>(this IServiceCollection services,
                                                              Func<TService, IServiceProvider, TService> decorator)
            where TService : class
        {
            // Bản đăng ký CUỐI CÙNG mới là bản mà container thực sự trả về khi resolve một service
            // đơn lẻ. ServiceKey is null để chỉ lấy đăng ký không khóa: các provider keyed
            // (Groq/Gemini, các chiến lược router) là những instance khác, có chủ đích không bọc.
            var descriptor = services.LastOrDefault(service =>
                                 service.ServiceType == typeof(TService) && service.ServiceKey is null)
                ?? throw new InvalidOperationException(
                    $"Chưa có đăng ký không khóa nào cho {typeof(TService).Name} nên không bọc được. " +
                    "Việc bọc phải diễn ra SAU khi module sở hữu service đó đã đăng ký — kiểm tra thứ tự trong AddRagStack.");

            services.Remove(descriptor);

            // Giữ NGUYÊN Lifetime của bản gốc. Nâng một singleton lên thành transient ở đây sẽ dựng
            // lại cả cây phụ thuộc bên dưới cho mỗi lần resolve — với QdrantVectorStore hay
            // RedisSemanticAnswerCache thì đó là một kết nối mới mỗi request.
            services.Add(ServiceDescriptor.Describe(
                typeof(TService),
                serviceProvider => decorator(CreateInner<TService>(serviceProvider, descriptor), serviceProvider),
                descriptor.Lifetime));

            return services;
        }

        /// <summary>
        /// Dựng instance gốc từ descriptor đã gỡ ra. Ba nhánh tương ứng ba cách đăng ký mà repo này
        /// đang dùng: <c>AddSingleton&lt;IFace, Impl&gt;()</c>, <c>AddSingleton&lt;IFace&gt;(sp =&gt; ...)</c>,
        /// và <c>AddSingleton&lt;IFace&gt;(instance)</c>.
        /// </summary>
        private static TService CreateInner<TService>(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
            where TService : class
        {
            if (descriptor.ImplementationInstance is not null)
                return (TService)descriptor.ImplementationInstance;

            if (descriptor.ImplementationFactory is not null)
                return (TService)descriptor.ImplementationFactory(serviceProvider);

            // ActivatorUtilities chứ không phải GetRequiredService: kiểu cài đặt thường KHÔNG được
            // đăng ký dưới chính nó, nên resolve thẳng sẽ ném.
            return (TService)ActivatorUtilities.CreateInstance(
                serviceProvider,
                descriptor.ImplementationType
                    ?? throw new InvalidOperationException(
                        $"Đăng ký của {typeof(TService).Name} không có instance, factory lẫn kiểu cài đặt nên không bọc được."));
        }
    }
}
