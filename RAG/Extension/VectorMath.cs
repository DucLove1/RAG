namespace RAG.Extension
{
    /// <summary>
    /// Các phép toán vector thuần túy dùng cho node định tuyến ngữ nghĩa.
    /// Toàn bộ hàm đều KHÔNG làm thay đổi mảng đầu vào: vector câu hỏi còn được dùng lại để truy hồi Qdrant.
    /// </summary>
    public static class VectorMath
    {
        /// <summary>
        /// Cosine similarity đầy đủ (có chia độ dài). Cố tình KHÔNG giả định vector đã chuẩn hóa L2
        /// vì Gemini không chuẩn hóa khi output_dimensionality bị cắt bớt so với số chiều gốc.
        /// Trả về 0 khi lệch số chiều hoặc vector rỗng/không có độ dài — fail-open, vì 0 không bao giờ vượt ngưỡng.
        /// </summary>
        public static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length == 0 || a.Length != b.Length)
                return 0d;

            double dot = 0d, normA = 0d, normB = 0d;
            for (int i = 0; i < a.Length; i++)
            {
                double x = a[i], y = b[i];
                dot += x * y;
                normA += x * x;
                normB += y * y;
            }

            if (normA <= 0d || normB <= 0d)
                return 0d;

            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        /// <summary>
        /// Vector có độ dài khác 0 hay không. Dùng để loại vector rác khi nạp cache:
        /// GeminiEmbeddingProvider trả mảng rỗng/giá trị 0 khi API lỗi thay vì ném exception.
        /// </summary>
        public static bool HasMagnitude(ReadOnlySpan<float> vector)
        {
            foreach (var value in vector)
                if (value != 0f) return true;

            return false;
        }
    }
}
