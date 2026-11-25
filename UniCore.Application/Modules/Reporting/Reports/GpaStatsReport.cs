using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // The spread of student GPAs by department. The GPA here is the credit-weighted average of the student's Published and
    // Locked grades in the range; the repeat policy is NOT applied (the official figure is the transcript's GPA).
    public sealed class GpaStatsReport(IAcademicReportData data, IReportScopeResolver scopes) : IReportProvider
    {
        public const string Code = "ACADEMIC_GPA_STATS";

        // Band edges (a GPA equal to the edge falls in the higher band).
        public const decimal PassEdge = 2.0m;
        public const decimal GoodEdge = 3.0m;
        public const decimal HonorsEdge = 3.5m;

        private static readonly ReportColumn[] Columns =
        [
            new("department", "Department", ReportColumnType.Text),
            new("students", "Students", ReportColumnType.Integer),
            new("averageGpa", "Average GPA", ReportColumnType.Decimal),
            new("lowestGpa", "Lowest GPA", ReportColumnType.Decimal),
            new("highestGpa", "Highest GPA", ReportColumnType.Decimal),
            new("below2", "Below 2.0", ReportColumnType.Integer),
            new("from2To3", "2.0 to 2.99", ReportColumnType.Integer),
            new("from3To35", "3.0 to 3.49", ReportColumnType.Integer),
            new("from35", "3.5 and above", ReportColumnType.Integer)
        ];

        private readonly IAcademicReportData _data = data;
        private readonly IReportScopeResolver _scopes = scopes;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "GPA statistics",
            "Students, average, lowest and highest GPA and the GPA bands by department. Credit-weighted over published and locked grades; the repeat policy is not applied here. Date filter: the day the grade was published. Only the departments in your scope.",
            AcademicReportSupport.Category,
            Permissions.AcademicReports.Gpa,
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
            var loaded = await _data.GetStudentGpaStatsAsync(from, toExclusive, cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            var scope = await _scopes.ResolveAsync(Permissions.AcademicReports.Gpa, AcademicReportSupport.DepartmentScopes(stats.Select(s => s.DepartmentId)), cancellationToken);

            return ReportTable.Paged(Columns, Build(stats, scope), page);
        }

        public static decimal GpaOf(StudentGpaStat stat) => Math.Round(stat.QualityPoints / stat.Credits, 2, MidpointRounding.AwayFromZero);

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<StudentGpaStat> stats, ReportScope scope)
        {
            // A student with no credits has no GPA and is not counted.
            var selected = stats.Where(s => scope.Allows(s.DepartmentId) && s.Credits > 0).ToList();
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var department in selected.GroupBy(s => s.DepartmentName).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                rows.Add(Row(department.Key, department.Select(GpaOf).ToList()));
            }

            if (rows.Count > 0)
            {
                rows.Add(Row(AcademicReportSupport.TotalLabel, [.. selected.Select(GpaOf)]));
            }

            return rows;
        }

        private static object?[] Row(string label, IReadOnlyList<decimal> gpas)
            => new object?[]
            {
                label,
                gpas.Count,
                Math.Round(gpas.Average(), 2, MidpointRounding.AwayFromZero),
                gpas.Min(),
                gpas.Max(),
                gpas.Count(g => g < PassEdge),
                gpas.Count(g => g >= PassEdge && g < GoodEdge),
                gpas.Count(g => g >= GoodEdge && g < HonorsEdge),
                gpas.Count(g => g >= HonorsEdge)
            };
    }
}
