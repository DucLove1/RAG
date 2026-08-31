using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Null Object cho <see cref="IRouteUtteranceAdmin"/> khi chiến lược đang chạy không nhận diện
    /// route bằng vector — cụ thể là chiến lược định tuyến bằng LLM.
    /// <para>
    /// Tách khỏi <see cref="DisabledRouteUtteranceAdmin"/> chứ không gộp thành một lớp có cờ: hai
    /// tình huống này cần hai câu trả lời khác nhau cho người vận hành. "Router đang tắt" là lời
    /// mời bật nó lên; "chiến lược này không hỗ trợ" nghĩa là router vẫn đang làm việc bình thường,
    /// chỉ là câu mẫu thêm vào sẽ chẳng đi tới đâu — muốn dạy nó nhận diện tốt hơn thì sửa
    /// <c>Description</c> và <c>Utterances</c> trong cấu hình rồi khởi động lại.
    /// </para>
    /// </summary>
    public sealed class UnsupportedRouteUtteranceAdmin : IRouteUtteranceAdmin
    {
        public Task<RouteUpdateResult> AddUtterancesAsync(string routeName,
                                                          IReadOnlyList<string> utterances,
                                                          IReadOnlyList<float[]> vectors,
                                                          CancellationToken cancellationToken = default) =>
            Task.FromResult(RouteUpdateResult.NotSupported());
    }
}
