namespace UniCore.Application.Modules.Reporting.Dashboard
{
    // What is kept in the cache for a widget.
    public sealed record DashboardSnapshot(IReadOnlyList<DashboardMetric> Metrics, DateTimeOffset GeneratedAt);


}
