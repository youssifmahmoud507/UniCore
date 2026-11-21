using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Registrations;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // Enrollments by department and status (Registered, Dropped, Withdrawn, Completed) with the credits they carry.
    // Only the departments in the user's scope. Date filter: when the seat was taken.
    public sealed class EnrollmentStatsReport(IAcademicReportData data, IReportScopeResolver scopes) : IReportProvider
    {
        public const string Code = "ACADEMIC_ENROLLMENT_STATS";

        private static readonly ReportColumn[] Columns =
        [
            new("department", "Department", ReportColumnType.Text),
            new("status", "Status", ReportColumnType.Text),
            new("enrollments", "Enrollments", ReportColumnType.Integer),
            new("credits", "Credits", ReportColumnType.Integer)
        ];

        private readonly IAcademicReportData _data = data;
        private readonly IReportScopeResolver _scopes = scopes;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Enrollment statistics",
            "Enrollments by department and status, with their credits. Date filter: the day the seat was taken. Only the departments in your scope.",
            AcademicReportSupport.Category,
            Permissions.AcademicReports.Enrollment,
            false,
            [
                ReportParameters.From("Registered from"),
                ReportParameters.To("Registered to"),
                new("status", "Status", ReportParameterType.Choice, false, [.. Enum.GetNames<EnrollmentStatus>()])
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
            var loaded = await _data.GetEnrollmentStatsAsync(from, toExclusive, cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            var scope = await _scopes.ResolveAsync(Permissions.AcademicReports.Enrollment, AcademicReportSupport.DepartmentScopes(stats.Select(s => s.DepartmentId)), cancellationToken);

            return ReportTable.Paged(Columns, Build(stats, scope, arguments.Text("status")), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<EnrollmentStat> stats, ReportScope scope, string? status)
        {
            var selected = stats
                .Where(s => scope.Allows(s.DepartmentId))
                .Where(s => status is null || string.Equals(s.Status.ToString(), status, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var rows = new List<IReadOnlyList<object?>>();

            foreach (var group in selected.GroupBy(s => (s.DepartmentName, s.Status)).OrderBy(g => g.Key.DepartmentName, StringComparer.Ordinal).ThenBy(g => g.Key.Status))
            {
                rows.Add(new object?[] { group.Key.DepartmentName, group.Key.Status.ToString(), group.Sum(s => s.Enrollments), group.Sum(s => s.Credits) });
            }

            if (rows.Count > 0)
            {
                rows.Add(new object?[] { AcademicReportSupport.TotalLabel, null, selected.Sum(s => s.Enrollments), selected.Sum(s => s.Credits) });
            }

            return rows;
        }
    }
}
