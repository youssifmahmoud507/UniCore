using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    public sealed class SecurityDashboardWidget(IReportDataSource data, IClock clock) : IDashboardWidget
    {
        private readonly IReportDataSource _data = data;
        private readonly IClock _clock = clock;

        public string Code => "security";

        public string Title => "Security";

        public string Category => "Platform";

        public string Permission => Permissions.Audit.Read;

        public bool IsPerUser => false;

        public async Task<Result<IReadOnlyList<DashboardMetric>>> LoadAsync(CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var day = await _data.GetAuditStatsAsync(now.AddHours(-24), now.AddMinutes(1), cancellationToken);

            if (!day.TryGetValue(out var last24Hours))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(day.Errors);
            }

            var week = await _data.GetAuditStatsAsync(now.AddDays(-7), now.AddMinutes(1), cancellationToken);

            if (!week.TryGetValue(out var last7Days))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(week.Errors);
            }

            return Result<IReadOnlyList<DashboardMetric>>.Ok(Build(last24Hours, last7Days));
        }

        public static IReadOnlyList<DashboardMetric> Build(IReadOnlyList<AuditStat> last24Hours, IReadOnlyList<AuditStat> last7Days)
        {
            static int Count(IEnumerable<AuditStat> stats, string action) => stats.Where(s => s.Action == action).Sum(s => s.Count);

            return
            [
                new("failedLogins24h", "Failed logins (24 hours)", Count(last24Hours, AuditActions.AuthLoginFailed)),
                new("lockedAccounts24h", "Accounts locked (24 hours)", Count(last24Hours, AuditActions.AuthAccountLocked)),
                new("tokenReuse7d", "Refresh token reuse detected (7 days)", Count(last7Days, AuditActions.AuthTokenReuseDetected)),
                new("reportExports7d", "Report exports (7 days)", Count(last7Days, AuditActions.ReportExported))
            ];
        }
    }


}
