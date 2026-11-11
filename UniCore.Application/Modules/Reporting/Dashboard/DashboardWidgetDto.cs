namespace UniCore.Application.Modules.Reporting.Dashboard
{
    public sealed record DashboardWidgetDto(string Code, string Title, string Category, IReadOnlyList<DashboardMetric> Metrics, DateTimeOffset GeneratedAt);


}
