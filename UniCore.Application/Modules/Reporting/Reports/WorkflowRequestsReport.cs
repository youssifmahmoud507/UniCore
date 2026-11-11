using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // Requests by kind and status, and how long they take. Scoped: a dean sees the requests of the faculty, not of
    // the whole university.
    public sealed class WorkflowRequestsReport(IReportDataSource data, IReportScopeResolver scopes, IClock clock) : IReportProvider
    {
        public const string Code = "WORKFLOW_REQUESTS";
        public const string TotalLabel = "Total";

        private static readonly ReportColumn[] Columns =
        [
            new("definition", "Request type", ReportColumnType.Text),
            new("status", "Status", ReportColumnType.Text),
            new("requests", "Requests", ReportColumnType.Integer),
            new("averageDays", "Average days", ReportColumnType.Decimal)
        ];

        private readonly IReportDataSource _data = data;
        private readonly IReportScopeResolver _scopes = scopes;
        private readonly IClock _clock = clock;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Requests by status",
            "Requests by type and status. Average days: start to finish for finished requests, age so far for the others. Only the requests in your scope.",
            "Requests",
            Permissions.Requests.Read,
            false,
            [
                ReportParameters.From("Started from"),
                ReportParameters.To("Started to"),
                new("definition", "Request type code", ReportParameterType.Text, false, Description: "for example WITHDRAWAL_REQUEST")
            ],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var range = ReportRanges.Read(arguments);

            if (!range.TryGetValue(out var span))
            {
                return Result<ReportTable>.Fail(range.Errors);
            }

            var from = span.From is { } start ? ReportRanges.StartOf(start) : (DateTimeOffset?)null;
            var toExclusive = span.To is { } end ? ReportRanges.StartOfNext(end) : (DateTimeOffset?)null;
            var loaded = await _data.GetWorkflowsAsync(from, toExclusive, cancellationToken);

            if (!loaded.TryGetValue(out var facts))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            var dataScopes = facts.Where(f => f.ScopeType is { } && f.ScopeId is { }).Select(f => new ScopeRef(f.ScopeType!.Value, f.ScopeId!.Value));
            var scope = await _scopes.ResolveAsync(Permissions.Requests.Read, dataScopes, cancellationToken);

            return ReportTable.Paged(Columns, Build(facts, scope, _clock.UtcNow, arguments.Text("definition")), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<WorkflowFact> facts, ReportScope scope, DateTimeOffset now, string? definition)
        {
            var selected = facts
                .Where(f => scope.Allows(f.ScopeId))
                .Where(f => definition is null || string.Equals(f.DefinitionCode, definition, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var rows = new List<IReadOnlyList<object?>>();

            foreach (var group in selected.GroupBy(f => (f.DefinitionCode, f.Status)).OrderBy(g => g.Key.DefinitionCode, StringComparer.Ordinal).ThenBy(g => g.Key.Status))
            {
                rows.Add(new object?[] { group.Key.DefinitionCode, group.Key.Status.ToString(), group.Count(), AverageDays(group, now) });
            }

            if (rows.Count > 0)
            {
                rows.Add(new object?[] { TotalLabel, null, selected.Count, null });
            }

            return rows;
        }

        private static decimal AverageDays(IEnumerable<WorkflowFact> facts, DateTimeOffset now)
        {
            var days = facts.Select(f => ((f.CompletedAt ?? now) - f.StartedAt).TotalDays).Select(d => Math.Max(0d, d)).ToList();

            return days.Count == 0 ? 0m : Math.Round((decimal)days.Average(), 2);
        }

    }



}
