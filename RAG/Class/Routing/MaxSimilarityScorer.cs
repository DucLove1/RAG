using RAG.Extension;
using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Điểm của route = điểm CAO NHẤT trong các câu mẫu của nó, không phải trung bình.
    /// <para>
    /// Liệt kê nhiều câu mẫu là để phủ nhiều cách nói; lấy trung bình sẽ trừng phạt đúng những
    /// route được viết kỹ và làm các route có số câu mẫu khác nhau không còn so sánh được với
    /// cùng một ngưỡng.
    /// </para>
    /// </summary>
    public sealed class MaxSimilarityScorer : IRouteScorer
    {
        public double Score(IReadOnlyList<float[]> utteranceVectors, float[] questionEmbedding)
        {
            var max = 0d;

            foreach (var vector in utteranceVectors)
            {
                var score = VectorMath.CosineSimilarity(questionEmbedding, vector);
                if (score > max) max = score;
            }

            return max;
        }
    }
}
