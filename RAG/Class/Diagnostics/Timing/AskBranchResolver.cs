using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Diagnostics.Timing
{
    /// <summary>
    /// Suy ra nhánh nào đã chạy, từ chính các nhãn mà những decorator ở dưới đã gắn dọc đường.
    /// <para>
    /// Tách ra khỏi <see cref="TimedAskService"/> để hai đường trả lời — không streaming và
    /// streaming — không có hai bản sao của cùng một ràng buộc. Đây không phải là gộp cho gọn: quy
    /// tắc dưới đây phải khớp THỨ TỰ THOÁT SỚM của pipeline, nên sửa một bản mà quên bản kia sẽ gán
    /// sai nhánh ở đúng một trong hai endpoint, và không có gì báo.
    /// </para>
    /// </summary>
    internal static class AskBranchResolver
    {
        /// <summary>
        /// Khớp route thì pipeline thoát ngay và không truy hồi; còn lại là nhánh truy hồi.
        /// </summary>
        public static string Resolve(ILatencyTracker latency)
        {
            var route = latency.GetTag(LatencyTags.Route);

            if (route is not null && route != LatencyTagValues.None)
                return LatencyTagValues.BranchRouted;

            return LatencyTagValues.BranchRetrieval;
        }
    }
}
