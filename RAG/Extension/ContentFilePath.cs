namespace RAG.Extension
{
    /// <summary>
    /// Giải đường dẫn tới các file NGUỒN đi theo build (corpus, dữ liệu đồ thị).
    /// <para>
    /// Là ẢNH GƯƠNG của <see cref="AppDataPath"/>, và hai lớp tồn tại song song vì lý do của chúng
    /// ngược nhau. <c>AppDataPath</c> giải theo <c>ContentRootPath</c> vì nó phục vụ dữ liệu SINH RA
    /// LÚC CHẠY: cache phải sống qua <c>dotnet clean</c> và qua việc đổi Debug↔Release. Lớp này
    /// giải theo <c>AppContext.BaseDirectory</c> vì nó phục vụ file được <c>&lt;Content&gt;</c> chép
    /// sang output — chúng CHỈ tồn tại ở đó, và ở môi trường dev thì thư mục output khác hẳn thư mục
    /// project.
    /// </para>
    /// <para>
    /// Trong container hai giá trị trùng nhau (đều là <c>/app</c>) nên khác biệt này chỉ lộ ra ở máy
    /// dev — đúng kiểu sai lệch chạy được trên CI rồi chết trên máy người viết, hoặc ngược lại.
    /// </para>
    /// </summary>
    public static class ContentFilePath
    {
        public static string Resolve(string configuredPath) =>
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);
    }
}
