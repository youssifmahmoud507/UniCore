using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // What happened in the system: audit events per action, most frequent first. Without dates: the last 7 days.
    public sealed class AuditActivityReport(IReportDataSource data, IClock clock) : IReportProvider
    {
        public const string Code = "AUDIT_ACTIVITY";
        public const int DefaultDays = 7;
        public const int MaxDays = 90;

        private static readonly ReportColumn[] Columns =
        [
            new("action", "Action", ReportColumnType.Text),
            new("events", "Events", ReportColumnType.Integer),
            new("lastAt", "Last event (UTC)", ReportColumnType.DateTime)
        ];

        private readonly IReportDataSource _data = data;
        private readonly IClock _clock = clock;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Audit activity",
            "Audit events per action: logins, failures, data changes, exports. Without dates: the last 7 days. Up to 90 days.",
            "Platform",
            Permissions.Audit.Read,
            true,
            [ReportParameters.From("From"), ReportParameters.To("To")],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var range = ReportRanges.ReadWithDefault(arguments, _clock.UtcToday, DefaultDays, MaxDays);

            if (!range.TryGetValue(out var span))
            {
                return Result<ReportTable>.Fail(range.Errors);
            }

            var loaded = await _data.GetAuditStatsAsync(ReportRanges.StartOf(span.From), ReportRanges.StartOfNext(span.To), cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            return ReportTable.Paged(Columns, Build(stats), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<AuditStat> stats)
            => [.. stats.GroupBy(s => s.Action).Select(g => new { Action = g.Key, Events = g.Sum(s => s.Count), Last = g.Max(s => s.LastAt) }).OrderByDescending(s => s.Events).ThenBy(s => s.Action, StringComparer.Ordinal).Select(s => (IReadOnlyList<object?>)[s.Action, s.Events, s.Last])];
    }



}
