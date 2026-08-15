namespace RAG.Extension
{
    public static class TextChunker
    {
        public static IEnumerable<string> ChunkText(string text, int chunkSize, int overlap)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            // Giữ nguyên hàm Normalize của bạn
            //var cleanedText = Normalize(text);
            int start = 0;

            while (start < text.Length)
            {
                int end = Math.Min(start + chunkSize, text.Length);

                // Chỉ cố gắng cắt theo dấu câu nếu chưa chạm đáy văn bản
                if (end < text.Length)
                {
                    // Khoảng nhìn lại linh hoạt (Ví dụ: tối đa quét ngược lại 20% độ dài chunk hoặc tối đa 100 ký tự)
                    int lookbackRange = Math.Min(Math.Max(50, chunkSize / 5), end - start);

                    int lastPeriod = text.LastIndexOfAny(new[] { '.', '!', '?', '\n' }, end - 1, lookbackRange);

                    // Đảm bảo đoạn text sau khi co lại vẫn giữ được một độ dài hợp lý (ít nhất 50% chunkSize)
                    if (lastPeriod != -1 && lastPeriod > start + (chunkSize / 2))
                    {
                        end = lastPeriod + 1;
                    }
                }

                // Trích xuất chunk
                string chunk = text.Substring(start, end - start).Trim();
                if (!string.IsNullOrEmpty(chunk))
                {
                    yield return chunk;
                }

                // 🔥 SỬA LỖI VÒNG LẶP VÔ TẬN TẠI ĐÂY
                int nextStart = end - overlap;

                if (nextStart <= start)
                {
                    // Nếu overlap quá lớn khiến cấu trúc bị đứng yên hoặc thụt lùi, 
                    // bắt buộc phải nhảy toán loạn đến 'end' của chunk hiện tại để tiếp tục tiến lên.
                    start = end;
                }
                else
                {
                    start = nextStart;
                }
            }
        }

        static string Normalize(string text)
        {
            // Loại bỏ các ký tự đặc biệt, chỉ giữ lại chữ và số
            var normalized = new string(text.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            // Chuẩn hóa khoảng trắng: thay nhiều khoảng trắng bằng một khoảng trắng duy nhất
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }
    }
}
