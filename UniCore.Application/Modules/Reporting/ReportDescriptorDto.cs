namespace UniCore.Application.Modules.Reporting
{
    public sealed record ReportDescriptorDto(string Code, string Name, string Description, string Category, bool IsSensitive, IReadOnlyList<ReportParameter> Parameters, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<string> ExportFormats, bool CanExport);




}
