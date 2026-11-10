using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Class.Dto;
using RAG.Interface;

namespace RAG.Class.Answering
{
    /// <summary>
    /// Lõi của đường trả lời: chuẩn hóa → định tuyến → truy hồi → sinh câu trả lời.
    /// </summary>
    public sealed class AskPipeline : IAskService
    {
        private readonly ILLMProvider _llmProvider;
        private readonly ILlmProviderResolver _llmProviderResolver;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IAskContextBuilder _contextBuilder;
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly ISemanticRouter _semanticRouter;
        private readonly IConversationFormatter _conversationFormatter;
        private readonly PromptConfig _promptConfig;

        public AskPipeline(ILLMProvider llmProvider,
                           ILlmProviderResolver llmProviderResolver,
                           IEmbeddingProvider embeddingProvider,
                           IAskContextBuilder contextBuilder,
                           IQueryNormalizer queryNormalizer,
                           ISemanticRouter semanticRouter,
                           IConversationFormatter conversationFormatter,
                           IOptions<PromptConfig> promptConfig)
        {
            _llmProvider = llmProvider;
            _llmProviderResolver = llmProviderResolver;
            _embeddingProvider = embeddingProvider;
            _contextBuilder = contextBuilder;
            _queryNormalizer = queryNormalizer;
            _semanticRouter = semanticRouter;
            _conversationFormatter = conversationFormatter;
            _promptConfig = promptConfig.Value;
        }

        public async Task<AskResult> AskAsync(string npcName,
                                              string npcSystem,
                                              string question,
                                              ConversationHistory history,
                                              int topK,
                                              string? stage = null,
                                              CancellationToken cancellationToken = default)
        {
            // Client quên compact thì NPC chỉ quên bớt chuyện cũ, prompt không phình vô hạn.
            history = _conversationFormatter.Trim(history);

            // Node chuẩn hóa: mở rộng từ viết tắt / sửa chính tả trước khi định tuyến và dựng prompt.
            // Có lịch sử thì nó còn viết lại câu nối tiếp thành câu độc lập, nên mọi node phía sau
            // (định tuyến, nhúng, truy hồi) chỉ cần nhìn câu đã chuẩn hóa.
            var normalizedQuestion = await _queryNormalizer.NormalizeAsync(question, history, cancellationToken);

            // Node định tuyến chạy TRƯỚC bước nhúng: câu tán gẫu được trả lời thẳng và không tốn
            // lượt gọi API embedding nào. Bản trước nhúng trước rồi mới định tuyến, nên 100% câu
            // tán gẫu vẫn phải trả giá một lượt nhúng mà không dùng tới.
            // Không route nào khớp (null) thì mặc định đi đường truy hồi.
            var route = await _semanticRouter.RouteAsync(normalizedQuestion, cancellationToken);

            // Ngân sách token hỏi thẳng provider đang được inject (chính là provider chọn theo
            // LLM:Provider), nên con số trong prompt luôn đúng bằng con số API sẽ cắt.
            var lengthInstruction = _promptConfig.BuildLengthInstruction(_llmProvider.MaxOutputTokens);

            // Khớp route nghĩa là câu này KHÔNG hỏi nội dung game (chào hỏi, cảm ơn, tạm biệt, ngoài
            // phạm vi), nên trả lời mà không truy hồi.
            if (route is not null)
            {
                // Route có lời đáp cố định (vd. câu ngoài thế giới game → "không biết") thì trả
                // nguyên văn, không qua LLM để nó không có cơ hội lỡ trả lời.
                if (route.FixedReply is not null)
                    return new AskResult(route.FixedReply, WeakPointHit: false);

                var routedAnswer = await AnswerWithoutRetrievalAsync(
                    npcName, npcSystem, normalizedQuestion, history, route, lengthInstruction, cancellationToken);

                return new AskResult(routedAnswer, WeakPointHit: false);
            }

            // Từ đây trở xuống là đường nội dung game.
            var answer = await AnswerWithRetrievalAsync(
                npcName, npcSystem, normalizedQuestion, history, topK, stage, lengthInstruction, cancellationToken);

            return new AskResult(answer, WeakPointHit: false);
        }

        /// <summary>
        /// Nhánh tán gẫu: vẫn để LLM sinh câu trả lời theo đúng persona của NPC,
        /// chỉ khác là không kèm ngữ cảnh truy hồi nào nên không chạm tới kho vector.
        /// <para>
        /// Route tự khai <see cref="RouteMatch.Provider"/> / <see cref="RouteMatch.Model"/> thì
        /// dùng đúng provider/model đó thay cho provider mặc định của pipeline — cùng cách
        /// <c>LlmRouterConfig.Provider</c> tách provider phân loại khỏi provider trả lời.
        /// </para>
        /// </summary>
        private Task<string> AnswerWithoutRetrievalAsync(string npcName,
                                                         string npcSystem,
                                                         string question,
                                                         ConversationHistory history,
                                                         RouteMatch route,
                                                         string lengthInstruction,
                                                         CancellationToken cancellationToken)
        {
            var provider = route.Provider is { } key ? _llmProviderResolver.Resolve(key) : _llmProvider;
            var options = route.Model is null ? null : new LlmRequestOptions(route.Model);

            return provider.AskAsync(
                route.BuildSystemPrompt(npcName, npcSystem, lengthInstruction),
                _conversationFormatter.PrependToAnswerPrompt(history, route.BuildUserPrompt(question)),
                options,
                cancellationToken);
        }

        /// <summary>
        /// Nhánh RAG mặc định: nhúng câu hỏi, tìm ngữ cảnh trong kho vector rồi mới sinh câu trả lời.
        /// <para>
        /// Việc nhúng nằm ở đây chứ không ở đầu pipeline vì chỉ nhánh này mới cần tới vector.
        /// Chiến lược định tuyến bằng embedding cũng nhúng câu hỏi, nhưng nó đi qua decorator cache
        /// nên lần nhúng ở đây là một lần trúng cache — tổng vẫn đúng một lượt gọi API mỗi request.
        /// </para>
        /// </summary>
        private async Task<string> AnswerWithRetrievalAsync(string npcName,
                                                            string npcSystem,
                                                            string question,
                                                            ConversationHistory history,
                                                            int topK,
                                                            string? stage,
                                                            string lengthInstruction,
                                                            CancellationToken cancellationToken)
        {
            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(question, cancellationToken);

            var context = await _contextBuilder.BuildAsync(
                npcName, question, questionEmbedding, topK, stage, cancellationToken);

            return await _llmProvider.AskAsync(
                _promptConfig.BuildSystemPrompt(npcName, npcSystem, lengthInstruction),
                _conversationFormatter.PrependToAnswerPrompt(
                    history, _promptConfig.BuildUserPrompt(npcName, context.Text, context.Graph, question)),
                cancellationToken: cancellationToken);
        }
    }
}
