namespace RAG.Interface
{
    /// <summary>
    /// Quy tắc gộp điểm của nhiều câu mẫu thành một điểm duy nhất cho route.
    /// <para>
    /// Tách thành abstraction vì đây là quyết định thuật toán có thể tranh luận được, và đổi nó
    /// sẽ làm dịch chuyển toàn bộ ngưỡng. Cài đặt mặc định lấy giá trị LỚN NHẤT — xem
    /// <c>MaxSimilarityScorer</c> để biết vì sao không phải trung bình.
    /// </para>
    /// </summary>
    public interface IRouteScorer
    {
        /// <param name="utteranceVectors">Vector của các câu mẫu thuộc route.</param>
        /// <param name="questionEmbedding">Vector câu hỏi. KHÔNG bị sửa đổi.</param>
        double Score(IReadOnlyList<float[]> utteranceVectors, float[] questionEmbedding);
    }
}
