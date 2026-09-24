using RAG.Interface;

namespace RAG.Class.Caching
{
    /// <summary>
    /// Đưa việc GHI cache câu trả lời ra chạy nền, để câu trả lời về tay người chơi ngay mà không
    /// phải chờ cache ghi xong. Việc ĐỌC giữ nguyên, vì kết quả đọc quyết định có phải sinh câu trả
    /// lời hay không.
    /// <para>
    /// Là decorator chứ không sửa hai pipeline: <c>AskPipeline</c> và <c>AskStreamPipeline</c> cùng
    /// ghi cache (bản streaming ghi TRƯỚC khi sự kiện <c>done</c> được gửi), nên một chỗ bọc thay
    /// được hai chỗ sửa.
    /// </para>
    /// <para>
    /// <c>Task.Run</c> chứ không chỉ bỏ <c>await</c>: provider FAISS ghi ĐỒNG BỘ (khóa ghi rồi thêm
    /// vector vào index trong RAM) và chỉ trả về một task đã xong, nên bỏ <c>await</c> thì việc ghi
    /// vẫn chạy trên luồng của request.
    /// </para>
    /// <para>
    /// Hai hệ quả có chủ đích: lượt ghi dùng <see cref="CancellationToken.None"/> — người chơi ngắt
    /// kết nối sau khi đã có câu trả lời trọn vẹn thì câu đó vẫn đáng được ghi; và lỗi ghi chỉ vào
    /// log, vì không lưu được cache không phải lý do làm hỏng một câu trả lời đã gửi đi. Lượt ghi
    /// còn dở lúc tiến trình tắt thì mất — với một cache, đó chỉ là một lần trượt về sau.
    /// </para>
    /// </summary>
    public sealed class BackgroundSemanticAnswerCache : ISemanticAnswerCache
    {
        private readonly ISemanticAnswerCache _inner;
        private readonly ILogger<BackgroundSemanticAnswerCache> _logger;

        public BackgroundSemanticAnswerCache(ISemanticAnswerCache inner, ILogger<BackgroundSemanticAnswerCache> logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public Task<CachedAnswer?> TryGetAsync(SemanticAnswerQuery query, CancellationToken cancellationToken = default) =>
            _inner.TryGetAsync(query, cancellationToken);

        public Task SetAsync(SemanticAnswerQuery query,
                             string answer,
                             bool hasContext,
                             CancellationToken cancellationToken = default)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _inner.SetAsync(query, answer, hasContext, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Ghi cache câu trả lời chạy nền cho NPC {Npc} thất bại.", query.NpcName);
                }
            }, CancellationToken.None);

            return Task.CompletedTask;
        }
    }
}
