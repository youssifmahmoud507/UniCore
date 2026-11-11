using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    public sealed class NotificationsDashboardWidget(IReportDataSource data, IClock clock) : IDashboardWidget
    {
        private readonly IReportDataSource _data = data;
        private readonly IClock _clock = clock;

        public string Code => "notifications";

        public string Title => "Notifications";

        public string Category => "Platform";

        public string Permission => Permissions.Audit.Read;

        public bool IsPerUser => false;

        public async Task<Result<IReadOnlyList<DashboardMetric>>> LoadAsync(CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var loaded = await _data.GetDeliveryStatsAsync(now.AddHours(-24), now.AddMinutes(1), cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<IReadOnlyList<DashboardMetric>>.Fail(loaded.Errors);
            }

            return Result<IReadOnlyList<DashboardMetric>>.Ok(Build(stats));
        }

        public static IReadOnlyList<DashboardMetric> Build(IReadOnlyList<DeliveryStat> last24Hours)
        {
            int Count(params NotificationDeliveryStatus[] statuses) => last24Hours.Where(s => statuses.Contains(s.Status)).Sum(s => s.Count);

            return
            [
                new("sent24h", "Delivered (24 hours)", Count(NotificationDeliveryStatus.Sent)),
                new("waiting24h", "Waiting to be sent (24 hours)", Count(NotificationDeliveryStatus.Pending, NotificationDeliveryStatus.Queued)),
                new("failed24h", "Failed (24 hours)", Count(NotificationDeliveryStatus.Failed))
            ];
        }
    }


}
