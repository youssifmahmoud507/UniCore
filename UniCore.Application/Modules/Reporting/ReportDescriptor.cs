namespace UniCore.Application.Modules.Reporting
{
    // What a report is: who may run it, which filters it takes, which columns it returns.
    // Permission is checked together with Reports.Read. IsSensitive reports (student money, the audit log) leave a
    // REPORT_VIEWED audit entry every time they are run.
    public sealed record ReportDescriptor(string Code, string Name, string Description, string Category, string Permission, bool IsSensitive, IReadOnlyList<ReportParameter> Parameters, IReadOnlyList<ReportColumn> Columns);




}
