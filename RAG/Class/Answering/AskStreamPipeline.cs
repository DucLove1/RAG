using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Answering
{
    /// <summary>
    /// Bản streaming của đường trả lời. Cùng ba nhánh và cùng thứ tự thoát sớm với
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
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IVectorStore _vectorStore;
        private readonly IQueryNormalizer _queryNormalizer;
        private readonly ISemanticRouter _semanticRouter;
        private readonly IWeakPointDetector _weakPointDetector;
        private readonly ISemanticAnswerCache _answerCache;
        private readonly PromptConfig _promptConfig;

        /// <param name="llmProvider">
        /// CHỈ để đọc <see cref="ILLMProvider.MaxOutputTokens"/>. Ngân sách token là thuộc tính của
        /// provider chứ không phải của cách gọi, nên nó không được nhân bản sang
        /// <see cref="ILLMStreamProvider"/>. Hai tham số này giải ra CÙNG một instance.
        /// </param>
        public AskStreamPipeline(ILLMProvider llmProvider,
                                 ILLMStreamProvider llmStreamProvider,
                                 IEmbeddingProvider embeddingProvider,
                                 IVectorStore vectorStore,
                                 IQueryNormalizer queryNormalizer,
                                 ISemanticRouter semanticRouter,
                                 IWeakPointDetector weakPointDetector,
                                 ISemanticAnswerCache answerCache,
                                 IOptions<PromptConfig> promptConfig)
        {
            _llmProvider = llmProvider;
            _llmStreamProvider = llmStreamProvider;
            _embeddingProvider = embeddingProvider;
            _vectorStore = vectorStore;
            _queryNormalizer = queryNormalizer;
            _semanticRouter = semanticRouter;
            _weakPointDetector = weakPointDetector;
            _answerCache = answerCache;
            _promptConfig = promptConfig.Value;
        }

        public async IAsyncEnumerable<AskStreamEvent> AskStreamAsync(
            string npcName,
            string npcSystem,
            string question,
            int topK,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var normalizedQuestion = await _queryNormalizer.NormalizeAsync(question, cancellationToken);
            var route = await _semanticRouter.RouteAsync(normalizedQuestion, cancellationToken);
            var lengthInstruction = _promptConfig.BuildLengthInstruction(_llmProvider.MaxOutputTokens);

            // NHÁNH 1 — tán gẫu. Khớp route nghĩa là câu này KHÔNG hỏi nội dung game, nên bộ phát
            // hiện điểm yếu không chạy và cờ chắc chắn là false.
            if (route is not null)
            {
                yield return new AskStreamMetaEvent(WeakPointHit: false);

                var routedStream = _llmStreamProvider.AskStreamAsync(
                    route.BuildSystemPrompt(npcName, npcSystem, lengthInstruction),
                    route.BuildUserPrompt(normalizedQuestion),
                    cancellationToken: cancellationToken);

                await foreach (var chunk in routedStream.WithCancellation(cancellationToken))
                    yield return new AskStreamTokenEvent(chunk);

                yield break;
            }

            var weakPoint = await _weakPointDetector.DetectAsync(npcName, normalizedQuestion, cancellationToken);

            // NHÁNH 2 — điểm yếu. Lời thoại lấy NGUYÊN VĂN từ cấu hình, không qua LLM, nên không có
            // gì để stream. Phát nguyên cục thay vì băm nhỏ giả lập: hiệu ứng gõ máy chữ là quyết
            // định TRÌNH BÀY, thuộc về client — client có sẵn chuỗi trọn vẹn thì tự animate được,
            // và tốc độ gõ nên do người chơi chỉnh trong settings chứ không do độ trễ mạng quyết
            // định. Băm ở đây còn buộc đẻ thêm một tham số kích thước mảnh trong cấu hình cho một
            // hiệu ứng thuần thẩm mỹ, và cắt chuỗi tiếng Việt theo số ký tự thì có nguy cơ cắt ngay
            // giữa một cặp surrogate.
            if (weakPoint is not null)
            {
                yield return new AskStreamMetaEvent(WeakPointHit: true);

                // Lời thoại được phép RỖNG trong cấu hình. Khi đó luồng là meta rồi done, KHÔNG có
                // token nào — client phải chịu được trường hợp này.
                if (!string.IsNullOrEmpty(weakPoint.Reply))
                    yield return new AskStreamTokenEvent(weakPoint.Reply);

                yield break;
            }

            // NHÁNH 3 — truy hồi.
            var questionEmbedding = await _embeddingProvider.GetEmbeddingsAsync(normalizedQuestion, cancellationToken);

            var cacheQuery = new SemanticAnswerQuery(npcName, npcSystem, normalizedQuestion, questionEmbedding);

            var cached = await _answerCache.TryGetAsync(cacheQuery, cancellationToken);
            if (cached is not null)
            {
                // Câu trả lời đã có sẵn trọn vẹn, không có gì để stream. Cùng lý do với nhánh điểm
                // yếu ở trên: phát một mảnh duy nhất.
                yield return new AskStreamMetaEvent(WeakPointHit: false);
                yield return new AskStreamTokenEvent(cached.Answer);
                yield break;
            }

            var filter = VectorSearchFilter.Match(PayloadFields.NpcNames, npcName);

            var hits = await _vectorStore.SearchAsync(questionEmbedding, filter, topK, cancellationToken);

            var context = string.Join(
                _promptConfig.ContextSeparator,
                hits.Select(hit => hit.Payload[PayloadFields.Text]));

            // Sự kiện ĐẦU TIÊN của nhánh này phát ở ĐÂY chứ không sớm hơn, và đó là quyết định có
            // chủ đích. Tầng ghi ra dây không gửi một byte nào — kể cả header — trước khi sự kiện
            // đầu về tay nó; nên mọi thứ nằm phía trên dòng này (nhúng, tra cache, tìm vector) vẫn
            // còn biến được thành 429/503/500 kèm ProblemDetails y hệt api/query/ask.
            //
            // Phát meta ngay sau bước định tuyến thì header bay đi TRƯỚC bước nhúng, và từ giây đó
            // một EmbeddingUnavailableException không còn là 503 nữa mà tụt xuống thành một sự kiện
            // lỗi nằm trong thân một response 200 — đúng thứ mà việc trả đúng mã lỗi vừa được sửa
            // để tránh. Client không mất gì: weakPointHit chỉ có ích khi đã có chữ để vẽ.
            yield return new AskStreamMetaEvent(WeakPointHit: false);

            var builder = new StringBuilder();

            var answerStream = _llmStreamProvider.AskStreamAsync(
                _promptConfig.BuildSystemPrompt(npcName, npcSystem, lengthInstruction),
                _promptConfig.BuildUserPrompt(context, normalizedQuestion),
                cancellationToken: cancellationToken);

            await foreach (var chunk in answerStream.WithCancellation(cancellationToken))
            {
                builder.Append(chunk);
                yield return new AskStreamTokenEvent(chunk);
            }

            // Dòng này CHỈ chạy khi vòng lặp trên kết thúc bình thường. Người chơi ngắt giữa chừng
            // thì consumer dispose enumerator ngay tại một yield return, và thân iterator không bao
            // giờ chạy tới đây — nghĩa là câu trả lời CỤT không được ghi cache. Đó là hành vi mong
            // muốn chứ không phải thiếu sót: một câu cụt đóng băng vào cache sẽ được phục vụ nguyên
            // văn cho mọi câu hỏi gần nghĩa cho tới khi hết hạn, đúng loại lỗi mà cờ
            // CacheAnswersWithoutContext sinh ra để tránh. Ngữ nghĩa dispose của iterator cho ta
            // điều đó mà không cần một dòng if nào.
            await _answerCache.SetAsync(cacheQuery, builder.ToString(), hasContext: hits.Count > 0, cancellationToken);
        }
    }
}
