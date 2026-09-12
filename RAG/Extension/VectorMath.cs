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
        /// Trả về BẢN SAO đã chuẩn hóa L2 (độ dài 1). KHÔNG sửa mảng nguồn — vector câu hỏi còn
        /// được <c>AskPipeline</c> dùng lại để truy hồi Qdrant ngay sau lượt tra cache, nên sửa
        /// tại chỗ là làm hỏng truy hồi một cách hoàn toàn im lặng.
        /// <para>
        /// Tồn tại vì chỉ mục <c>IndexFlatIP</c> của FAISS chấm bằng tích vô hướng THUẦN. Nó CHỈ
        /// bằng cosine khi cả hai vector có độ dài 1, mà Gemini không chuẩn hóa khi
        /// <c>output_dimensionality</c> bị cắt bớt so với số chiều gốc (xem
        /// <see cref="CosineSimilarity"/>). Bỏ bước này thì điểm trả về là |a||b|cos, tức là ĐỘ
        /// DÀI vector chen vào xếp hạng — câu dài hay nhiều từ hiếm luôn thắng — và mọi điểm rời
        /// khỏi thang [0,1] nên ngưỡng cấu hình mất hết ý nghĩa. Không exception, không log.
        /// </para>
        /// <para>
        /// Chuẩn hóa BẢO TOÀN cosine (cosine bất biến với phép nhân vô hướng), nên lưu bản đã
        /// chuẩn hóa là an toàn: chấm điểm trên nó cho đúng con số như chấm trên vector gốc.
        /// </para>
        /// <para>
        /// Vector rỗng hoặc không có độ dài thì trả bản sao nguyên trạng — fail-open, vì điểm sẽ
        /// là 0 và không bao giờ vượt ngưỡng.
        /// </para>
        /// </summary>
        public static float[] L2Normalize(ReadOnlySpan<float> vector)
        {
            var result = vector.ToArray();

            double norm = 0d;
            foreach (var value in vector)
                norm += (double)value * value;

            if (norm <= 0d)
                return result;

            norm = Math.Sqrt(norm);

            for (var i = 0; i < result.Length; i++)
                result[i] = (float)(result[i] / norm);

            return result;
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
