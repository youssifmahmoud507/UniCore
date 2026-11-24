using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Reporting;

namespace UniCore.Application.Modules.Reporting.Reports
{
    // Seats and registrations per course section: capacity, enrolled, free seats and how full the section is. Scoped: a
    // department sees the sections of its courses. Counts only, never the students.
    public sealed class EnrollmentBySectionReport(IReportDataSource data, IReportScopeResolver scopes) : IReportProvider
    {
        public const string Code = "ENROLLMENT_BY_SECTION";
        public const string TotalLabel = "Total";

        private static readonly ReportColumn[] Columns =
        [
            new("courseCode", "Course code", ReportColumnType.Text),
            new("course", "Course", ReportColumnType.Text),
            new("section", "Section", ReportColumnType.Text),
            new("capacity", "Capacity", ReportColumnType.Integer),
            new("enrolled", "Enrolled", ReportColumnType.Integer),
            new("seatsLeft", "Seats left", ReportColumnType.Integer),
            new("fillPercent", "Full (%)", ReportColumnType.Decimal)
        ];

        private readonly IReportDataSource _data = data;
        private readonly IReportScopeResolver _scopes = scopes;

        public ReportDescriptor Descriptor { get; } = new(
            Code,
            "Enrollment by section",
            "Capacity, enrolled students and free seats of every section, with how full it is. Filter by semester. Only the sections in your scope.",
            "Registration",
            Permissions.Registration.Read,
            false,
            [new("semesterId", "Semester id", ReportParameterType.Text, false, Description: "the id of the semester; all semesters when empty")],
            Columns);

        public async Task<Result<ReportTable>> RunAsync(ReportArguments arguments, ReportPage page, CancellationToken cancellationToken = default)
        {
            Guid? semester = null;

            if (arguments.Text("semesterId") is { } text)
            {
                if (!Guid.TryParse(text, out var parsed))
                {
                    return ReportingErrors.ParameterInvalid("semesterId", "must be a valid id");
                }

                semester = parsed;
            }

            var loaded = await _data.GetSectionEnrollmentsAsync(semester, cancellationToken);

            if (!loaded.TryGetValue(out var facts))
            {
                return Result<ReportTable>.Fail(loaded.Errors);
            }

            var scope = await _scopes.ResolveAsync(Permissions.Registration.Read, facts.Select(f => f.DepartmentId).Distinct().Select(id => new ScopeRef(ScopeType.Department, id)), cancellationToken);

            return ReportTable.Paged(Columns, Build(facts, scope), page);
        }

        public static IReadOnlyList<IReadOnlyList<object?>> Build(IEnumerable<SectionEnrollmentFact> facts, ReportScope scope)
        {
            var selected = facts.Where(f => scope.Allows(f.DepartmentId)).ToList();

            var rows = selected
                .OrderBy(f => f.CourseCode, StringComparer.Ordinal)
                .ThenBy(f => f.SectionCode, StringComparer.Ordinal)
                .Select(f => (IReadOnlyList<object?>)new object?[] { f.CourseCode, f.CourseTitle, f.SectionCode, f.Capacity, f.Enrolled, Math.Max(0, f.Capacity - f.Enrolled), Fill(f.Enrolled, f.Capacity) })
                .ToList();

            if (rows.Count > 0)
            {
                var capacity = selected.Sum(f => f.Capacity);
                var enrolled = selected.Sum(f => f.Enrolled);
                rows.Add(new object?[] { TotalLabel, null, null, capacity, enrolled, Math.Max(0, capacity - enrolled), Fill(enrolled, capacity) });
            }

            return rows;
        }

        private static decimal Fill(int enrolled, int capacity) => capacity <= 0 ? 0m : Math.Round(enrolled * 100m / capacity, 2);
    }
}
