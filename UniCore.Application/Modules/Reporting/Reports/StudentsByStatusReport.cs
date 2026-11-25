using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // How many students are in each academic status (active, on leave, suspended, graduated...). Counts only. Scoped by the
    // department of the student's current program; students without a program are for global users.
    public sealed class StudentsByStatusReport(IReportDataSource data, IReportScopeResolver scopes) : IReportProvider
    {
        public const string Code = "STUDENTS_BY_STATUS";
        public const string TotalLabel = "Total";

        private static readonly ReportColumn[] Columns =
        [
            new("status", "Status", ReportColumnType.Text),
            new("students", "Students", ReportColumnType.Integer)
        ];

        private readonly IReportDataSource _data = data;
        private readonly IReportScopeResolver _scopes = scopes;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Students by status",
            "The number of students in each academic status. Only the students in your scope.",
            "Students",
            Permissions.Students.Read,
            false,
            [],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var loaded = await _data.GetStudentStatusStatsAsync(cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            var scope = await _scopes.ResolveAsync(Permissions.Students.Read, stats.Where(s => s.DepartmentId is { }).Select(s => s.DepartmentId!.Value).Distinct().Select(id => new ScopeRef(ScopeType.Department, id)), cancellationToken);

            return ReportTable.Paged(Columns, Build(stats, scope), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<StudentStatusStat> stats, ReportScope scope)
        {
            var selected = stats.Where(s => scope.Allows(s.DepartmentId)).ToList();

            var rows = selected
                .GroupBy(s => s.Status)
                .OrderBy(g => g.Key)
                .Select(g => (IReadOnlyList<object?>)new object?[] { g.Key.ToString(), g.Sum(s => s.Count) })
                .ToList();

            if (rows.Count > 0)
            {
                rows.Add(new object?[] { TotalLabel, selected.Sum(s => s.Count) });
            }

            return rows;
        }
    }
}
