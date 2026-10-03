using System.Text.Json.Serialization;

namespace RAG.Class.Dto
{
    // --- DTO map theo chuẩn POST /v1/systemone của TypeSafe (mô hình quyết định Jev) ---
    //
    // Chỉ map phần router dùng tới: câu hỏi Choice và đáp án của nó. Noul / Score có hình dạng
    // khác và chưa có ai dùng, nên chưa map.

    public sealed record SystemOneRequest
    {
        [JsonPropertyName("model")] public required string Model { get; init; }

        /// <summary>Nội dung cần phán đoán — ở đây là câu của người chơi đã chuẩn hóa.</summary>
        [JsonPropertyName("state")] public required string State { get; init; }

        [JsonPropertyName("questions")] public required IReadOnlyDictionary<string, SystemOneQuestion> Questions { get; init; }
    }

    public sealed record SystemOneQuestion
    {
        [JsonPropertyName("type")] public required string Type { get; init; }

        [JsonPropertyName("instructions")] public required string Instructions { get; init; }

        /// <summary>
        /// Tên option → mô tả. Giá trị là <c>string</c> hoặc <see cref="SystemOneCriterion"/>; khai
        /// kiểu <c>object</c> để System.Text.Json serialize theo kiểu thật lúc chạy.
        /// <para>
        /// Dùng <see cref="OrderedDictionary{TKey, TValue}"/> chứ không phải <c>Dictionary</c>: THỨ
        /// TỰ option ảnh hưởng tới đáp án của Jev, và <c>Dictionary</c> không cam kết giữ thứ tự chèn.
        /// </para>
        /// </summary>
        [JsonPropertyName("criteria")] public required OrderedDictionary<string, object> Criteria { get; init; }
    }

    /// <summary>
    /// Mô tả có cấu trúc của một option — tài liệu Jev khuyến nghị dạng này khi các option gần
    /// nhau về nghĩa (chào hỏi / tạm biệt / cảm ơn).
    /// </summary>
    public sealed record SystemOneCriterion
    {
        [JsonPropertyName("what")] public required string What { get; init; }

        [JsonPropertyName("examples")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? Examples { get; init; }
    }

    public sealed record SystemOneResponse
    {
        [JsonPropertyName("model")] public string? Model { get; init; }

        [JsonPropertyName("answers")] public Dictionary<string, SystemOneAnswer>? Answers { get; init; }

        [JsonPropertyName("usage")] public SystemOneUsage? Usage { get; init; }
    }

    /// <summary>Đáp án của một câu hỏi Choice.</summary>
    public sealed record SystemOneAnswer
    {
        [JsonPropertyName("type")] public string? Type { get; init; }

        /// <summary>Tên option thắng.</summary>
        [JsonPropertyName("choice")] public string? Choice { get; init; }

        /// <summary>Độ tin cậy đã hiệu chỉnh, trong [0, 1].</summary>
        [JsonPropertyName("confidence")] public double Confidence { get; init; }

        /// <summary>Xác suất của từng option.</summary>
        [JsonPropertyName("probabilities")] public Dictionary<string, double>? Probabilities { get; init; }
    }

    public sealed record SystemOneUsage
    {
        [JsonPropertyName("input_tokens")] public int InputTokens { get; init; }

        [JsonPropertyName("output_tokens")] public int OutputTokens { get; init; }
    }
}
