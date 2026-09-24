namespace RAG.Class.Constants
{
    /// <summary>
    /// Vị trí và quy ước đặt tên của các file cấu hình tách khỏi <c>appsettings.json</c>.
    /// <para>
    /// Là hằng số chứ không phải cấu hình vì chúng được dùng TRƯỚC khi cấu hình tồn tại: đây là thứ
    /// quyết định nạp file nào.
    /// </para>
    /// </summary>
    public static class ConfigurationFiles
    {
        /// <summary>Thư mục chứa các file cấu hình con, giải theo <c>ContentRootPath</c> như appsettings.json.</summary>
        public const string Directory = "Config";

        public const string SearchPattern = "*.json";

        public const string Extension = ".json";

        /// <summary>File gốc mà các file con được chèn ngay sau nó trong thứ tự ưu tiên.</summary>
        public const string BaseFileName = "appsettings.json";

        /// <summary>Hậu tố file ghi đè theo môi trường: <c>graph.Development.json</c>.</summary>
        public const string EnvironmentSuffixTemplate = ".{0}" + Extension;

        public const char NameSeparator = '.';
    }
}
