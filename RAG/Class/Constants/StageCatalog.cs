namespace RAG.Class.Constants
{
    /// <summary>
    /// Mã số stage dùng trên API và trong corpus, kèm THỨ TỰ của chúng.
    /// <para>
    /// Một bảng duy nhất cho cả hai chiều: API gửi số (<c>Stage: 2</c>), corpus ghi tên
    /// (<c>[stage:2]</c>), và luật "biết mọi tri thức của stage ≤ hiện tại" cần đi ngược từ tên về
    /// số. Hai bảng rời thì sớm muộn sẽ lệch nhau một dòng, và dòng lệch đó lặng lẽ thành tri thức
    /// không bao giờ hé lộ.
    /// </para>
    /// </summary>
    public static class StageCatalog
    {
        private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
        {
            [1] = "1",
            [2] = "2",
            [3] = "3"
        };

        private static readonly IReadOnlyDictionary<string, int> Orders =
            Names.ToDictionary(entry => entry.Value, entry => entry.Key, StringComparer.Ordinal);

        /// <summary>Mã số trên API → tên stage trong corpus; <c>null</c> khi không gửi hoặc không có trong bảng.</summary>
        public static string? Resolve(int? stage) =>
            stage is { } value && Names.TryGetValue(value, out var name) ? name : null;

        /// <summary>Tên stage trong corpus → thứ tự của nó; <c>null</c> khi tên lạ.</summary>
        public static int? Order(string? stageName) =>
            stageName is not null && Orders.TryGetValue(stageName, out var order) ? order : null;

        /// <summary>
        /// Mọi tên stage đã được hé lộ ở <paramref name="currentStage"/> (thứ tự ≤ hiện tại), theo
        /// thứ tự tăng dần. Dùng cho kho vector, nơi luật tích lũy phải đi vào bộ lọc dưới dạng một
        /// danh sách giá trị chứ không phải một hàm so sánh. Stage lạ cho danh sách rỗng — tức là chỉ
        /// còn các dòng không gắn stage, cùng chiều "giữ kín" với <see cref="IsRevealedAt"/>.
        /// </summary>
        public static IReadOnlyList<string> RevealedAt(string currentStage) =>
            Order(currentStage) is { } current
                ? Names.Where(entry => entry.Key <= current).OrderBy(entry => entry.Key).Select(entry => entry.Value).ToList()
                : Array.Empty<string>();

        /// <summary>
        /// Luật TÍCH LŨY: ở stage hiện tại, một dòng đã được hé lộ khi nó không gắn stage, hoặc gắn
        /// một stage có thứ tự ≤ stage hiện tại.
        /// <para>
        /// Tên lạ ở BẤT KỲ phía nào đều cho <c>false</c> (với dòng có gắn stage): không so được thứ
        /// tự thì giữ kín. Đoán sai theo chiều mở là rò tri thức của màn sau; đoán sai theo chiều
        /// đóng chỉ là NPC tạm quên một dòng.
        /// </para>
        /// </summary>
        public static bool IsRevealedAt(string? lineStage, string currentStage)
        {
            if (lineStage is null)
                return true;

            return Order(lineStage) is { } line
                && Order(currentStage) is { } current
                && line <= current;
        }
    }
}
