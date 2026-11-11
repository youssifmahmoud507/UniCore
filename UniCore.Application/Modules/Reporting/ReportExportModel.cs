namespace UniCore.Application.Modules.Reporting
{
    public sealed record ReportExportModel(string Title, string Description, string Parameters, DateTimeOffset GeneratedAt, ReportTable Table);




}
