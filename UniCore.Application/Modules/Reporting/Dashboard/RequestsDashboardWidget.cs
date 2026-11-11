using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    // Requests in the user's scope.
    public sealed class RequestsDashboardWidget(IReportDataSource data, IReportScopeResolver scopes, IClock clock) : IDashboardWidget
    {
        public const int RecentDays = 30;

        private readonly IReportDataSource _data = data;
        private readonly IReportScopeResolver _scopes = scopes;
        private readonly IClock _clock = clock;

        public string Code => "requests";

        public string Title => "Requests";

        public string Category => "Requests";

        public string Permission => Permissions.Requests.Read;

        public bool IsPerUser => true;

        public async Task<Result<IReadOnlyList<DashboardMetric>>> LoadAsync(CancellationToken cancellationToken = default)
        {
            var loaded = await _data.GetWorkflowStatsAsync(_clock.UtcNow.AddDays(-RecentDays), cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(loaded.Errors);
            }

            var dataScopes = stats.Where(s => s.ScopeType is { } && s.ScopeId is { }).Select(s => new ScopeRef(s.ScopeType!.Value, s.ScopeId!.Value));
            var scope = await _scopes.ResolveAsync(Permissions.Requests.Read, dataScopes, cancellationToken);

            return Result<IReadOnlyList<DashboardMetric>>.Ok(Build(stats, scope));
        }

        public static IReadOnlyList<DashboardMetric> Build(IEnumerable<WorkflowStat> stats, ReportScope scope)
        {
            var visible = stats.Where(s => scope.Allows(s.ScopeId)).ToList();

            int Count(WorkflowInstanceStatus status) => visible.Where(s => s.Status == status).Sum(s => s.Count);

            return
            [
                new("inProgress", "In progress", Count(WorkflowInstanceStatus.InProgress)),
                new("returned", "Returned to the requester", Count(WorkflowInstanceStatus.Returned)),
                new("completedRecently", $"Approved in the last {RecentDays} days", Count(WorkflowInstanceStatus.Completed)),
                new("rejectedRecently", $"Rejected in the last {RecentDays} days", Count(WorkflowInstanceStatus.Rejected))
            ];
        }
    }


}
