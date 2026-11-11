namespace UniCore.Application.Modules.Reporting.Dashboard
{
    // Widgets the user may see, and the codes of the ones that could not be loaded right now (one broken widget must
    // not hide the others).
    public sealed record DashboardDto(IReadOnlyList<DashboardWidgetDto> Widgets, IReadOnlyList<string> Unavailable);


}
