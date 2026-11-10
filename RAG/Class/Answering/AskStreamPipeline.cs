using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;

namespace RAG.Class.Answering
{
    /// <summary>
    /// Bản streaming của đường trả lời. Cùng hai nhánh và cùng thứ tự thoát sớm với
    /// <see cref="AskPipeline"/>, chỉ khác ở chỗ câu trả lời đi ra từng mảnh.
    /// <para>
    /// CỐ Ý là một lớp song song chứ không phải <see cref="AskPipeline"/> được tổng quát hóa. Hai
    /// đường có hình dạng trả về khác hẳn nhau (một giá trị so với một luồng), và nhồi chung sẽ đẻ
    /// ra một lớp mà mỗi nhánh phải hỏi "mình đang chạy ở chế độ nào" — đúng thứ làm cho một thay
    /// đổi ở nhánh này âm thầm phá nhánh kia. Cái giá phải trả là một ràng buộc: ĐỔI THỨ TỰ NHÁNH Ở
    /// ĐÂY THÌ PHẢI ĐỔI CẢ <see cref="AskPipeline"/>. Bài đối chiếu ask với ask-stream trong
    /// RAG.http tồn tại để bắt đúng lúc hai bên lệch nhau.
    /// </para>
    /// <para>
    /// KHÔNG sinh sự kiện done/error: chúng là khung TRUYỀN, thuộc về tầng ghi ra dây. Lớp này
    /// không biết HTTP là gì.
    /// </para>
    /// </summary>
    public sealed class AskStreamPipeline : IAskStreamService
    {
        private readonly ILLMProvider _llmProvider;
        private readonly ILLMStreamProvider _llmStreamProvider;
        private readonly ILlmProviderResolver _llmProviderResolver;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IAskContextBuilder _contextBuilder;
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly ISemanticRouter _semanticRouter;
        private readonly IConversationFormatter _conversationFormatter;
        private readonly PromptConfig _promptConfig;

        /// <param name="llmProvider">
        /// CHỈ để đọc <see cref="ILLMProvider.MaxOutputTokens"/>. Ngân sách token là thuộc tính của
        /// provider chứ không phải của cách gọi, nên nó không được nhân bản sang
        /// <see cref="ILLMStreamProvider"/>. Hai tham số này giải ra CÙNG một instance.
        /// </param>
        public AskStreamPipeline(ILLMProvider llmProvider,
                                 ILLMStreamProvider llmStreamProvider,
                                 ILlmProviderResolver llmProviderResolver,
                                 IEmbeddingProvider embeddingProvider,
                                 IAskContextBuilder contextBuilder,
                                 IQueryNormalizer queryNormalizer,
                                 ISemanticRouter semanticRouter,
                                 IConversationFormatter conversationFormatter,
                                 IOptions<PromptConfig> promptConfig)
        {
            _conversationFormatter = conversationFormatter;
            _llmProvider = llmProvider;
            _llmStreamProvider = llmStreamProvider;
            _llmProviderResolver = llmProviderResolver;
            _embeddingProvider = embeddingProvider;
            _contextBuilder = contextBuilder;
            _queryNormalizer = queryNormalizer;
            _semanticRouter = semanticRouter;
            _promptConfig = promptConfig.Value;
        }

        public async IAsyncEnumerable<AskStreamEvent> AskStreamAsync(
            string npcName,
            string npcSystem,
            string question,
            ConversationHistory history,
            int topK,
            string? stage = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            history = _conversationFormatter.Trim(history);

            var normalizedQuestion = await _queryNormalizer.NormalizeAsync(question, history, cancellationToken);
            var route = await _semanticRouter.RouteAsync(normalizedQuestion, cancellationToken);
            var lengthInstruction = _promptConfig.BuildLengthInstruction(_llmProvider.MaxOutputTokens);

            // NHÁNH 1 — tán gẫu. Khớp route nghĩa là câu này KHÔNG hỏi nội dung game.
            if (route is not null)
            {
                // Lời đáp cố định lấy NGUYÊN VĂN từ cấu hình, không qua LLM, nên không có gì để
                // stream: phát nguyên cục. Hiệu ứng gõ máy chữ là quyết định TRÌNH BÀY, thuộc về
                // client — băm nhỏ ở đây còn buộc đẻ thêm một tham số kích thước mảnh trong cấu
                // hình, và cắt chuỗi tiếng Việt theo số ký tự thì có nguy cơ cắt giữa một cặp surrogate.
                if (route.FixedReply is not null)
                {
                    yield return new AskStreamTokenEvent(route.FixedReply);
                    yield break;
                }

                // Route tự khai Provider/Model thì dùng đúng provider/model đó thay cho provider
                // mặc định của pipeline — cùng cách AskPipeline.AnswerWithoutRetrievalAsync làm.
                var routedStreamProvider = route.Provider is { } key
                    ? _llmProviderResolver.ResolveStream(key)
                    : _llmStreamProvider;
                var routedOptions = route.Model is null ? null : new LlmRequestOptions(route.Model);

                var routedStream = routedStreamProvider.AskStreamAsync(
                    route.BuildSystemPrompt(npcName, npcSystem, lengthInstruction),
                    _conversationFormatter.PrependToAnswerPrompt(history, route.BuildUserPrompt(normalizedQuestion)),
                    routedOptions,
                    cancellationToken);

                await foreach (var chunk in routedStream.WithCancellation(cancellationToken))
                    yield return new AskStreamTokenEvent(chunk);

                yield break;
            }

            // NHÁNH 2 — truy hồi.
            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(normalizedQuestion, cancellationToken);

            var context = await _contextBuilder.BuildAsync(
                npcName, normalizedQuestion, questionEmbedding, topK, stage, cancellationToken);

            // Không có sự kiện nào phát trước mảnh văn bản đầu tiên, và đó là có chủ đích. Tầng ghi
            // ra dây không gửi một byte nào — kể cả header — trước khi sự kiện đầu về tay nó; nên
            // mọi thứ nằm phía trên (nhúng, tìm vector, mở kết nối tới LLM) vẫn còn biến được thành
            // 429/503/500 kèm ProblemDetails y hệt api/query/ask. Phát một sự kiện nào đó sớm hơn
            // thì header bay đi TRƯỚC các bước đó, và một EmbeddingUnavailableException không còn là
            // 503 nữa mà tụt xuống thành một sự kiện lỗi nằm trong thân một response 200.
            var answerStream = _llmStreamProvider.AskStreamAsync(
                _promptConfig.BuildSystemPrompt(npcName, npcSystem, lengthInstruction),
                _conversationFormatter.PrependToAnswerPrompt(
                    history, _promptConfig.BuildUserPrompt(npcName, context.Text, context.Graph, normalizedQuestion)),
                cancellationToken: cancellationToken);

            await foreach (var chunk in answerStream.WithCancellation(cancellationToken))
                yield return new AskStreamTokenEvent(chunk);
        }
    }
}
