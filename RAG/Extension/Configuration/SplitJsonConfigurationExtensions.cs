using Microsoft.Extensions.Configuration.Json;
using RAG.Class.Constants;

namespace RAG.Extension.Configuration
{
    /// <summary>
    /// Nạp các file cấu hình trong thư mục <see cref="ConfigurationFiles.Directory"/> — mỗi file một
    /// nhóm section, thay cho một appsettings.json dài hàng trăm dòng.
    /// </summary>
    public static class SplitJsonConfigurationExtensions
    {
        /// <summary>
        /// Chèn các file con vào NGAY SAU source <c>appsettings.json</c> chứ không thêm vào cuối.
        /// <para>
        /// Thứ tự thành: appsettings.json → Config/*.json → Config/*.{Env}.json → appsettings.{Env}.json
        /// → user secrets → biến môi trường → command line. Thêm vào cuối thì file con sẽ ĐÈ LÊN biến
        /// môi trường và appsettings.{Env}.json — mọi <c>ENV Section__Key</c> trong Dockerfile và trên
        /// Render lặng lẽ mất tác dụng.
        /// </para>
        /// <para>
        /// File <c>&lt;tên&gt;.{Env}.json</c> chỉ được nạp khi đúng môi trường đang chạy; file của môi
        /// trường khác bị bỏ qua.
        /// </para>
        /// </summary>
        public static IConfigurationManager AddSplitJsonFiles(this IConfigurationManager configuration, IHostEnvironment environment)
        {
            var directory = Path.Combine(environment.ContentRootPath, ConfigurationFiles.Directory);
            if (!Directory.Exists(directory))
            {
                return configuration;
            }

            var files = Directory.GetFiles(directory, ConfigurationFiles.SearchPattern)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var environmentSuffix = string.Format(ConfigurationFiles.EnvironmentSuffixTemplate, environment.EnvironmentName);
            var baseFiles = files.Where(IsBaseFile);
            var environmentFiles = files.Where(f => Path.GetFileName(f).EndsWith(environmentSuffix, StringComparison.OrdinalIgnoreCase));

            var sources = baseFiles.Select(f => CreateSource(f, optional: false))
                .Concat(environmentFiles.Select(f => CreateSource(f, optional: true)))
                .ToList();

            // Không tìm thấy appsettings.json thì đặt ở đầu: ưu tiên thấp nhất, mọi source khác vẫn đè được.
            var insertAt = FindBaseSourceIndex(configuration.Sources) + 1;
            for (var i = 0; i < sources.Count; i++)
            {
                configuration.Sources.Insert(insertAt + i, sources[i]);
            }

            return configuration;
        }

        private static bool IsBaseFile(string path) =>
            !Path.GetFileNameWithoutExtension(path).Contains(ConfigurationFiles.NameSeparator);

        private static JsonConfigurationSource CreateSource(string path, bool optional)
        {
            var source = new JsonConfigurationSource
            {
                Path = path,
                Optional = optional,
                ReloadOnChange = true,
            };
            source.ResolveFileProvider();
            return source;
        }

        private static int FindBaseSourceIndex(IList<IConfigurationSource> sources)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i] is JsonConfigurationSource json
                    && string.Equals(json.Path, ConfigurationFiles.BaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
