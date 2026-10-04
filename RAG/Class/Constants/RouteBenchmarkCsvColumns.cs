namespace RAG.Class.Constants
{
    /// <summary>Tên cột trong file CSV của benchmark định tuyến. Ánh xạ theo tên, không theo vị trí.</summary>
    public static class RouteBenchmarkCsvColumns
    {
        public const string Id = "Id";
        public const string Question = "Question";
        public const string ExpectedRoute = "ExpectedRoute";
        public const string Tag = "Tag";
        public const string Note = "Note";

        public const string Delimiter = ",";
    }
}
