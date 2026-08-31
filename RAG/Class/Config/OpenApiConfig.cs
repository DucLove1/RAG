using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Config
{
    /// <summary>
    /// Cấu hình tài liệu OpenAPI. Đường dẫn trước đây là chuỗi "/openapi" nằm cứng trong Program.cs.
    /// </summary>
    public class OpenApiConfig
    {
        public const string SectionName = "OpenApi";

        [Required(AllowEmptyStrings = false)]
        public string RoutePattern { get; set; } = "/openapi";
    }
}
