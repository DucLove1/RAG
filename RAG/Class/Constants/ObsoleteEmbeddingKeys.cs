namespace RAG.Class.Constants
{
    /// <summary>
    /// Bẫy di trú cấu hình: section của node nhúng đã đổi tên từ <c>Gemini</c> thành
    /// <c>EmbeddingModel</c>, để nó không còn đọc như "mọi thứ thuộc về Gemini" trong khi
    /// <c>GEMINILLM</c> nằm ngay bên cạnh cũng là Gemini.
    /// <para>
    /// Phải nổ lúc khởi động chứ không thể chỉ ghi log. API key của node nhúng đến từ biến môi
    /// trường (<c>GEMINI__APIKEYS__0</c> trên máy dev và trên dashboard Render): giữ tên cũ thì
    /// chúng bind vào hư không, và triệu chứng là một lỗi <c>ApiKeys không được trống</c> chẳng
    /// liên quan gì tới việc vừa đổi tên section — người vận hành sẽ đi tìm cái key bị mất.
    /// </para>
    /// <para>
    /// Câu chữ nằm ở đây chứ không ở configuration là có chủ ý, cùng lý do với
    /// <see cref="ObsoleteRouterKeys"/>: đây là thông báo đọc lúc app từ chối khởi động, nên nó
    /// phải đọc được kể cả khi file cấu hình sai.
    /// </para>
    /// </summary>
    public static class ObsoleteEmbeddingKeys
    {
        /// <summary>Tên section cũ. Còn tồn tại nghĩa là cấu hình chưa được đổi tên.</summary>
        public const string ObsoleteSectionName = "Gemini";

        public const string Message =
            "Section cấu hình \"Gemini\" (node nhúng văn bản) đã đổi tên thành \"EmbeddingModel\", " +
            "nhưng section cũ vẫn đang được đặt nên chắc chắn có chỗ chưa đổi. Hãy đổi tên:\n" +
            "  - appsettings.json: \"Gemini\": { ... }  ->  \"EmbeddingModel\": { ... }\n" +
            "  - biến môi trường:  GEMINI__APIKEYS__0   ->  EMBEDDINGMODEL__APIKEYS__0\n" +
            "                      GEMINI__<Tên>        ->  EMBEDDINGMODEL__<Tên>\n" +
            "Nếu đang deploy bằng Docker/Render thì đổi luôn trên dashboard, nếu không node nhúng " +
            "sẽ khởi động mà không có API key nào.";
    }
}
