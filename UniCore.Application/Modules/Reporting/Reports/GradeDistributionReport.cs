using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // How many grades of each letter, by department. Published and Locked grades only: a draft or an unapproved grade
    // never shows up here. Date filter: when the grade was published.
    public sealed class GradeDistributionReport(IAcademicReportData data, IReportScopeResolver scopes) : IReportProvider
    {
        public const string Code = "ACADEMIC_GRADE_DISTRIBUTION";

        private static readonly ReportColumn[] Columns =
        [
            new("department", "Department", ReportColumnType.Text),
            new("letter", "Letter", ReportColumnType.Text),
            new("grades", "Grades", ReportColumnType.Integer),
            new("share", "Share of department (%)", ReportColumnType.Decimal),
            new("averagePercentage", "Average percentage", ReportColumnType.Decimal)
        ];

        private readonly IAcademicReportData _data = data;
        private readonly IReportScopeResolver _scopes = scopes;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Grade distribution",
            "Grades by department and letter, with their share and average percentage. Published and locked grades only. Date filter: the day the grade was published. Only the departments in your scope.",
            AcademicReportSupport.Category,
            Permissions.AcademicReports.Grades,
            false,
            [ReportParameters.From("Published from"), ReportParameters.To("Published to")],
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
            var loaded = await _data.GetGradeStatsAsync(from, toExclusive, cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            var scope = await _scopes.ResolveAsync(Permissions.AcademicReports.Grades, AcademicReportSupport.DepartmentScopes(stats.Select(s => s.DepartmentId)), cancellationToken);

            return ReportTable.Paged(Columns, Build(stats, scope), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<GradeStat> stats, ReportScope scope)
        {
            var selected = stats.Where(s => scope.Allows(s.DepartmentId)).ToList();
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var department in selected.GroupBy(s => s.DepartmentName).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var total = department.Sum(s => s.Grades);

                // Best letters first (highest average), then by letter so the order is stable.
                foreach (var letter in department.GroupBy(s => s.Letter).OrderByDescending(g => Average(g)).ThenBy(g => g.Key, StringComparer.Ordinal))
                {
                    var count = letter.Sum(s => s.Grades);
                    rows.Add(new object?[] { department.Key, letter.Key, count, AcademicReportSupport.Percent(count, total), Average(letter) });
                }
            }

            if (rows.Count > 0)
            {
                rows.Add(new object?[] { AcademicReportSupport.TotalLabel, null, selected.Sum(s => s.Grades), null, null });
            }

            return rows;
        }

        private static decimal Average(IEnumerable<GradeStat> stats)
        {
            var count = stats.Sum(s => s.Grades);

            return count == 0 ? 0m : Math.Round(stats.Sum(s => s.PercentageSum) / count, 2, MidpointRounding.AwayFromZero);
        }
    }
}
