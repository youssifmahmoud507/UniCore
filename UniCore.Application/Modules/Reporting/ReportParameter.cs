namespace UniCore.Application.Modules.Reporting
{
    public sealed record ReportParameter(string Name, string Label, ReportParameterType Type, bool Required = false, IReadOnlyList<string>? Choices = null, string? Description = null);




}
