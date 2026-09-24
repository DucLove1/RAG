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
        public static IServiceCollection AddRagStack(this IServiceCollection services,
                                                     IConfiguration configuration,
                                                     IHostEnvironment environment)
        {
            // Các provider LLM dạng Keyed Services (Groq / Gemini).
            services.AddLLM(configuration);

            // Cache đường hỏi đáp. PHẢI đăng ký TRƯỚC các thành phần bị bọc (embedding, chuẩn hóa).
            services.AddQueryCache(configuration);

            services.AddEmbeddingModel(configuration);

            // Kho vector (Qdrant qua gRPC).
            services.AddQdrant(configuration);

            // Đồ thị tri thức (GraphRAG). Ràng buộc thứ tự DUY NHẤT là phải đứng TRƯỚC
            // AddLatencyTracking, vì dòng đó BỌC IGraphSearch và IGraphEntityExtractor, và Decorate<>
            // ném ngay lúc khởi động nếu chưa có gì để bọc.
            //
            // Vị trí ngay sau AddQdrant là để ĐỌC, không phải một ràng buộc: tầng này đi cặp với kho
            // vector (hai nhánh chạy song song trong AskContextBuilder, và IChunkTextLookup của Qdrant
            // tra ngược nguyên văn các dòng mà cạnh đồ thị trỏ tới), nên để cạnh nhau thì người đọc
            // thấy ngay quan hệ đó. Container giải phụ thuộc theo nhu
            // cầu chứ không theo thứ tự đăng ký, nên nó phụ thuộc được vào ICorpusIngestionService
            // đăng ký mãi dưới AddIngestion mà không sao.
            services.AddGraph(configuration, environment);

            // Cache câu trả lời theo ngữ nghĩa (FAISS trong tiến trình, hoặc Redis + RediSearch —
            // chọn bằng SemanticAnswerCache:Provider). KHÔNG phải decorator — khác AddQueryCache ở
            // trên vốn phải đứng trước thứ nó bọc. Ràng buộc thứ tự duy nhất ở đây là phải nằm
            // TRƯỚC AddRagPipeline, vì AskPipeline nhận ISemanticAnswerCache qua constructor.
            // Provider FAISS còn đọc GeminiEmbeddingModelConfig để chốt số chiều, nên AddEmbeddingModel
            // ở trên cũng là một ràng buộc thứ tự thật.
            services.AddSemanticAnswerCache(configuration);

            // Node chuẩn hóa câu hỏi người dùng (viết tắt, sai chính tả, thiếu dấu).
            services.AddQueryNormalization(configuration);

            // Node định tuyến ngữ nghĩa: nhận diện câu tán gẫu để trả lời thẳng, bỏ qua truy hồi.
            services.AddSemanticRouter(configuration);

            // Node phát hiện "trúng điểm yếu": chạy SAU định tuyến, và trúng thì THẮNG route vì nó
            // là sự kiện cốt truyện chứ không phải một tối ưu chi phí. Phải sau AddLLM (cần
            // ILlmProviderResolver) và trước AddRagPipeline (AskPipeline nhận IWeakPointDetector).
            services.AddWeakPoint(configuration);

            // Đường nạp tri thức: bộ đọc theo định dạng + chiến lược cắt đoạn.
            services.AddIngestion(configuration);

            // Façade pipeline; phụ thuộc vào mọi thứ ở trên.
            services.AddRagPipeline(configuration);

            // ProblemDetails + ánh xạ exception nghiệp vụ sang mã HTTP đúng nghĩa.
            services.AddErrorHandling(configuration);

            // Đo độ trễ từng chặng. PHẢI ĐỨNG CUỐI CÙNG: khác mọi dòng trên, dòng này không đăng ký
            // service mới mà BỌC những service đã đăng ký ở trên. Đẩy nó lên trên thì nó bọc vào
            // khoảng không — và sẽ ném ngay lúc khởi động chứ không âm thầm mất số liệu.
            services.AddLatencyTracking(configuration);

            return services;
        }
    }
}
