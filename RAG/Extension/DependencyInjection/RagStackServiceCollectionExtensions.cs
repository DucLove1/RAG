namespace RAG.Extension.DependencyInjection
{
    /// <summary>
    /// Điểm vào duy nhất để đăng ký toàn bộ stack RAG.
    /// <para>
    /// THỨ TỰ Ở ĐÂY LÀ QUAN TRỌNG và đó chính là lý do method này tồn tại. Cache phải được đăng ký
    /// trước các thành phần bị nó bọc. Trước đây thứ tự nằm rải trong <c>Program.cs</c> và chỉ được
    /// bảo vệ bằng một dòng comment — ai sắp xếp lại cho "gọn" là làm hỏng, mà không có gì báo.
    /// Gói vào một method thì thứ tự được khóa trong code và có một chỗ duy nhất để giải thích.
    /// </para>
    /// </summary>
    public static class RagStackServiceCollectionExtensions
    {
        public static IServiceCollection AddRagStack(this IServiceCollection services, IConfiguration configuration)
        {
            // Các provider LLM dạng Keyed Services (Groq / Gemini).
            services.AddLLM(configuration);

            // Cache đường hỏi đáp. PHẢI đăng ký TRƯỚC các thành phần bị bọc (embedding, chuẩn hóa).
            services.AddQueryCache(configuration);

            services.AddEmbeddingModel(configuration);

            // Kho vector (Qdrant qua gRPC).
            services.AddQdrant(configuration);

            // Node chuẩn hóa câu hỏi người dùng (viết tắt, sai chính tả, thiếu dấu).
            services.AddQueryNormalization(configuration);

            // Node định tuyến ngữ nghĩa: nhận diện câu tán gẫu để trả lời thẳng, bỏ qua truy hồi.
            services.AddSemanticRouter(configuration);

            // Đường nạp tri thức: bộ đọc theo định dạng + chiến lược cắt đoạn.
            services.AddIngestion(configuration);

            // Façade pipeline; phụ thuộc vào mọi thứ ở trên.
            services.AddRagPipeline(configuration);

            // ProblemDetails + ánh xạ exception nghiệp vụ sang mã HTTP đúng nghĩa.
            services.AddErrorHandling(configuration);

            return services;
        }
    }
}
