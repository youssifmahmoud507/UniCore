using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // Attendance marks by department. Closed sessions only (an open session is not final). The rate follows the
    // attendance rule: Present, Excused and Late count as attended, Absent does not. A late arrival beyond a course's own
    // late threshold is counted by the course report, not here. Date filter: the day of the session.
    public sealed class AttendanceStatsReport(IAcademicReportData data, IReportScopeResolver scopes) : IReportProvider
    {
        public const string Code = "ACADEMIC_ATTENDANCE_STATS";

        private static readonly ReportColumn[] Columns =
        [
            new("department", "Department", ReportColumnType.Text),
            new("records", "Marks", ReportColumnType.Integer),
            new("present", "Present", ReportColumnType.Integer),
            new("late", "Late", ReportColumnType.Integer),
            new("excused", "Excused", ReportColumnType.Integer),
            new("absent", "Absent", ReportColumnType.Integer),
            new("rate", "Attendance rate (%)", ReportColumnType.Decimal)
        ];

        private readonly IAcademicReportData _data = data;
        private readonly IReportScopeResolver _scopes = scopes;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Attendance statistics",
            "Marks by department: present, late, excused, absent and the attendance rate. Closed sessions only. Date filter: the day of the session. Only the departments in your scope.",
            AcademicReportSupport.Category,
            Permissions.AcademicReports.Attendance,
            false,
            [ReportParameters.From("Session from"), ReportParameters.To("Session to")],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            var range = ReportRanges.Read(arguments);

            if (!range.TryGetValue(out var span))
            {
                return Result<ReportTable>.Fail(range.Errors);
            }

            var loaded = await _data.GetAttendanceStatsAsync(span.From, span.To, cancellationToken);

            if (!loaded.TryGetValue(out var stats))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            var scope = await _scopes.ResolveAsync(Permissions.AcademicReports.Attendance, AcademicReportSupport.DepartmentScopes(stats.Select(s => s.DepartmentId)), cancellationToken);

            return ReportTable.Paged(Columns, Build(stats, scope), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<AttendanceStat> stats, ReportScope scope)
        {
            var selected = stats.Where(s => scope.Allows(s.DepartmentId)).ToList();
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var department in selected.GroupBy(s => s.DepartmentName).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                rows.Add(Row(department.Key, department));
            }

            if (rows.Count > 0)
            {
                rows.Add(Row(AcademicReportSupport.TotalLabel, selected));
            }

            return rows;
        }

        private static object?[] Row(string label, IEnumerable<AttendanceStat> stats)
        {
            var list = stats.ToList();
            var total = list.Sum(s => s.Records);
            var present = Count(list, "Present");
            var late = Count(list, "Late");
            var excused = Count(list, "Excused");
            var absent = Count(list, "Absent");

            return new object?[] { label, total, present, late, excused, absent, AcademicReportSupport.Percent(present + late + excused, total) };
        }

        private static int Count(IEnumerable<AttendanceStat> stats, string status)
            => stats.Where(s => string.Equals(s.Status, status, StringComparison.OrdinalIgnoreCase)).Sum(s => s.Records);
    }
}
