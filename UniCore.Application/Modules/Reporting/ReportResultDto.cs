namespace UniCore.Application.Modules.Reporting
{
    public sealed record ReportResultDto(string Code, string Name, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, int Page, int PageSize, int TotalRows, DateTimeOffset GeneratedAt);




}
