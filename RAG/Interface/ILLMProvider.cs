namespace RAG.Interface
{
    public interface ILLMProvider
    {
        /// <summary>
        /// Trần token đầu ra mà provider này thực sự gửi lên API. Lộ ra ở đây để tầng dựng prompt
        /// nhắm đúng con số API sẽ cắt, thay vì giữ một bản sao riêng rồi lệch dần theo thời gian.
        /// </summary>
        int MaxOutputTokens { get; }

        /// <summary>
        /// <paramref name="model"/> để trống thì provider dùng model mặc định của chính nó
        /// (cấu hình Model trong section provider). Cho phép mỗi consumer (chuẩn hóa, router...)
        /// chọn model riêng mà không cần thêm provider hay pool API key mới.
        /// </summary>
        Task<string> AskAsync(string system, string user, string? model = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Sinh câu trả lời theo LUỒNG, từng mảnh một.
    /// <para>
    /// Tách khỏi <see cref="ILLMProvider"/> theo ISP. Ba consumer còn lại của tầng LLM — bộ chuẩn
    /// hóa, bộ định tuyến, bộ phát hiện điểm yếu — đều đưa output vào một parser rồi mới dùng, nên
    /// chúng cần chuỗi NGUYÊN KHỐI và sẽ không bao giờ gọi tới đây. Gộp vào một interface là bắt
    /// chúng nhìn thấy một method vô nghĩa với mình, và bắt mọi provider tương lai phải cài nó.
    /// </para>
    /// <para>
    /// BẤT BIẾN về vị trí lỗi: cài đặt PHẢI mở kết nối và lấy mảnh đầu tiên TRƯỚC khi <c>yield</c>
    /// bất cứ thứ gì. Nhờ vậy mọi lỗi có thể xoay API key (429) và mọi lỗi kết nối đều nổ ra trong
    /// lần <c>MoveNextAsync</c> ĐẦU TIÊN của consumer — tức là trước khi tầng HTTP kịp ghi byte nào,
    /// và vẫn còn trở thành mã lỗi thật được. Mở kết nối lười sau mảnh đầu là đẩy những lỗi đó
    /// xuống thành một sự kiện lỗi bên trong một response 200.
    /// </para>
    /// <para>
    /// Mảnh trả về là delta thô của model: KHÔNG <c>Trim()</c> từng mảnh. Đường không streaming
    /// <c>Trim()</c> cả chuỗi một lần, còn trim từng mảnh sẽ ăn mất khoảng trắng giữa hai token và
    /// câu trả lời dính chữ vào nhau.
    /// </para>
    /// </summary>
    public interface ILLMStreamProvider
    {
        IAsyncEnumerable<string> AskStreamAsync(string system,
                                                string user,
                                                string? model = null,
                                                CancellationToken cancellationToken = default);
    }
}
