namespace UniCore.Application.Modules.Reporting
{
    // The rows of one page, and how many rows the whole report has.
    public sealed record ReportTable(IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, int TotalRows)
    {
        // For reports that build all their rows in memory: keeps the page asked for.
        public static ReportTable Paged(IReadOnlyList<ReportColumn> columns, IReadOnlyList<IReadOnlyList<object?>> allRows, ReportPage page)
            => new(columns, [.. allRows.Skip(Math.Max(0, page.Skip)).Take(Math.Max(0, page.Take))], allRows.Count);
    }




}
