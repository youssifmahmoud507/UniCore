namespace UniCore.Application.Modules.Reporting
{
    // A cell of the column is null, string, int, long, decimal, bool, DateOnly or DateTimeOffset, matching Type.
    public sealed record ReportColumn(string Key, string Label, ReportColumnType Type);




}
