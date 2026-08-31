using System.ComponentModel.DataAnnotations;

namespace RAG.Class.Validation
{
    /// <summary>
    /// Chuỗi phải có nội dung thực sự, không chỉ khoảng trắng.
    /// <para>
    /// Cần một attribute riêng vì <see cref="RequiredAttribute"/> chấp nhận chuỗi toàn khoảng trắng
    /// (" " không phải null và không rỗng), trong khi mọi chỗ trong ứng dụng đều kiểm bằng
    /// <c>IsNullOrWhiteSpace</c>. Khai báo được thì kiểm tra nằm cạnh định nghĩa DTO thay vì lặp lại
    /// trong từng action, và thông báo lỗi do framework sinh nên không có literal nào trong code.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class NotBlankAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value) =>
            value is string text && !string.IsNullOrWhiteSpace(text);
    }
}
