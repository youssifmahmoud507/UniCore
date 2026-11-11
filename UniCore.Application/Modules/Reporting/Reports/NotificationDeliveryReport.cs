using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // Did the notifications arrive: deliveries per channel and status. Without dates: the last 7 days.
    public sealed class NotificationDeliveryReport(IReportDataSource data, IClock clock) : IReportProvider
    {
        public const string Code = "NOTIFICATION_DELIVERY";
        public const int DefaultDays = 7;
        public const int MaxDays = 90;

        private static readonly ReportColumn[] Columns =
        [
            new("channel", "Channel", ReportColumnType.Text),
            new("status", "Status", ReportColumnType.Text),
            new("deliveries", "Deliveries", ReportColumnType.Integer),
            new("share", "Share of channel (%)", ReportColumnType.Decimal)
        ];

        private readonly IReportDataSource _data = data;
        private readonly IClock _clock = clock;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Notification delivery",
            "Deliveries per channel and status (sent, queued, pending, failed). Without dates: the last 7 days. Up to 90 days.",
            "Platform",
            Permissions.Audit.Read,
            false,
            [ReportParameters.From("Created from"), ReportParameters.To("Created to")],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var range = ReportRanges.ReadWithDefault(arguments, _clock.UtcToday, DefaultDays, MaxDays);

            if (!range.TryGetValue(out var span))
            {
                return Result<ReportTable>.Fail(range.Errors);
            }

            var loaded = await _data.GetDeliveryStatsAsync(ReportRanges.StartOf(span.From), ReportRanges.StartOfNext(span.To), cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            return ReportTable.Paged(Columns, Build(stats), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<DeliveryStat> stats)
        {
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var channel in stats.GroupBy(s => s.Channel).OrderBy(g => g.Key))
            {
                var total = channel.Sum(s => s.Count);

                foreach (var status in channel.GroupBy(s => s.Status).OrderBy(g => g.Key))
                {
                    var count = status.Sum(s => s.Count);

                    rows.Add(new object?[] { channel.Key.ToString(), status.Key.ToString(), count, total == 0 ? 0m : Math.Round(count * 100m / total, 2) });
                }
            }

            return rows;
        }
    }



}
