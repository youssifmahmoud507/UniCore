using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Reports;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Reporting;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Identity;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;

namespace UniCore.UnitTests.Modules.Reporting
{
    internal sealed class FakeAcademicReportData : IAcademicReportData
    {
        public List<EnrollmentStat> Enrollments { get; } = [];
        public List<GradeStat> Grades { get; } = [];
        public List<StudentGpaStat> Gpas { get; } = [];
        public List<AttendanceStat> Attendance { get; } = [];
        public Error? Fail { get; set; }

        // What the last call asked for.
        public (DateTimeOffset? From, DateTimeOffset? ToExclusive) EnrollmentRange { get; private set; }
        public (DateTimeOffset? From, DateTimeOffset? ToExclusive) GradeRange { get; private set; }
        public (DateOnly? From, DateOnly? To) AttendanceRange { get; private set; }

        public Task<Result<IReadOnlyList<EnrollmentStat>>> GetEnrollmentStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default)
        {
            EnrollmentRange = (from, toExclusive);

            return Task.FromResult(Fail is { } error ? Result<IReadOnlyList<EnrollmentStat>>.Fail(error) : Result<IReadOnlyList<EnrollmentStat>>.Ok([.. Enrollments]));
        }

        public Task<Result<IReadOnlyList<GradeStat>>> GetGradeStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default)
        {
            GradeRange = (from, toExclusive);

            return Task.FromResult(Fail is { } error ? Result<IReadOnlyList<GradeStat>>.Fail(error) : Result<IReadOnlyList<GradeStat>>.Ok([.. Grades]));
        }

        public Task<Result<IReadOnlyList<StudentGpaStat>>> GetStudentGpaStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default)
            => Task.FromResult(Fail is { } error ? Result<IReadOnlyList<StudentGpaStat>>.Fail(error) : Result<IReadOnlyList<StudentGpaStat>>.Ok([.. Gpas]));

        public Task<Result<IReadOnlyList<AttendanceStat>>> GetAttendanceStatsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        {
            AttendanceRange = (from, to);

            return Task.FromResult(Fail is { } error ? Result<IReadOnlyList<AttendanceStat>>.Fail(error) : Result<IReadOnlyList<AttendanceStat>>.Ok([.. Attendance]));
        }
    }

    public class AcademicStatsReportsTests
    {
        private static readonly Guid Computing = Guid.NewGuid();
        private static readonly Guid Business = Guid.NewGuid();

        private static ReportScope Only(Guid department) => new(false, new HashSet<Guid> { department });

        private static ReportArguments Dates(string from, string to)
            => new(new Dictionary<string, object> { ["from"] = DateOnly.Parse(from), ["to"] = DateOnly.Parse(to) });

        private static List<IReportProvider> Providers(ReportingEnv env, FakeAcademicReportData data)
        {
            var scopes = new ReportScopeResolver(env.Access, env.Hierarchy);

            return
            [
                new EnrollmentStatsReport(data, scopes),
                new GradeDistributionReport(data, scopes),
                new GpaStatsReport(data, scopes),
                new AttendanceStatsReport(data, scopes)
            ];
        }

        // ---------- the four reports ----------

        [Fact]
        public void Enrollments_are_summed_by_department_and_status_with_a_total()
        {
            var rows = EnrollmentStatsReport.Build(
            [
                new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Registered, 40, 120),
                new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Dropped, 5, 15),
                new EnrollmentStat(Business, "Business", EnrollmentStatus.Registered, 10, 30)
            ], ReportScope.Everything, null);

            Assert.Equal(4, rows.Count);
            Assert.Equal(new object?[] { "Business", "Registered", 10, 30 }, rows[0].ToArray());
            Assert.Equal(new object?[] { "Computing", "Registered", 40, 120 }, rows[1].ToArray());
            Assert.Equal(new object?[] { "Computing", "Dropped", 5, 15 }, rows[2].ToArray());
            Assert.Equal(new object?[] { "Total", null, 55, 165 }, rows[3].ToArray());
        }

        [Fact]
        public void Enrollments_can_be_limited_to_one_status_in_any_letter_case()
        {
            var rows = EnrollmentStatsReport.Build(
            [
                new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Registered, 40, 120),
                new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Dropped, 5, 15)
            ], ReportScope.Everything, "dropped");

            Assert.Equal(2, rows.Count);
            Assert.Equal("Dropped", rows[0][1]);
            Assert.Equal(5, rows[1][2]);
        }

        [Fact]
        public void Grades_show_each_letters_share_of_its_department_best_letter_first()
        {
            var rows = GradeDistributionReport.Build(
            [
                new GradeStat(Computing, "Computing", "A", 10, 950m),
                new GradeStat(Computing, "Computing", "B", 30, 2400m),
                new GradeStat(Computing, "Computing", "F", 10, 300m)
            ], ReportScope.Everything);

            Assert.Equal(4, rows.Count);
            Assert.Equal(new object?[] { "Computing", "A", 10, 20m, 95m }, rows[0].ToArray());
            Assert.Equal(new object?[] { "Computing", "B", 30, 60m, 80m }, rows[1].ToArray());
            Assert.Equal(new object?[] { "Computing", "F", 10, 20m, 30m }, rows[2].ToArray());
            Assert.Equal(new object?[] { "Total", null, 50, null, null }, rows[3].ToArray());
        }

        [Fact]
        public void Gpa_is_weighted_by_credits_and_students_fall_into_bands_with_the_edge_in_the_higher_band()
        {
            var rows = GpaStatsReport.Build(
            [
                new StudentGpaStat(Computing, "Computing", 18m, 6),   // 3.00 -> 3.0 to 3.49
                new StudentGpaStat(Computing, "Computing", 12m, 6),   // 2.00 -> 2.0 to 2.99
                new StudentGpaStat(Computing, "Computing", 10m, 6),   // 1.67 -> below 2.0
                new StudentGpaStat(Computing, "Computing", 21m, 6),   // 3.50 -> 3.5 and above
                new StudentGpaStat(Computing, "Computing", 0m, 0)     // no credits: not counted
            ], ReportScope.Everything);

            Assert.Equal(2, rows.Count);
            Assert.Equal(new object?[] { "Computing", 4, 2.54m, 1.67m, 3.5m, 1, 1, 1, 1 }, rows[0].ToArray());
            Assert.Equal("Total", rows[1][0]);
            Assert.Equal(4, rows[1][1]);
        }

        [Fact]
        public void Attendance_rate_counts_present_late_and_excused_as_attended()
        {
            var rows = AttendanceStatsReport.Build(
            [
                new AttendanceStat(Computing, "Computing", "Present", 70),
                new AttendanceStat(Computing, "Computing", "Late", 10),
                new AttendanceStat(Computing, "Computing", "Excused", 5),
                new AttendanceStat(Computing, "Computing", "Absent", 15)
            ], ReportScope.Everything);

            Assert.Equal(2, rows.Count);
            Assert.Equal(new object?[] { "Computing", 100, 70, 10, 5, 15, 85m }, rows[0].ToArray());
            Assert.Equal(new object?[] { "Total", 100, 70, 10, 5, 15, 85m }, rows[1].ToArray());
        }

        [Fact]
        public void Reports_with_no_data_have_no_rows_and_no_total()
        {
            Assert.Empty(EnrollmentStatsReport.Build([], ReportScope.Everything, null));
            Assert.Empty(GradeDistributionReport.Build([], ReportScope.Everything));
            Assert.Empty(GpaStatsReport.Build([], ReportScope.Everything));
            Assert.Empty(AttendanceStatsReport.Build([], ReportScope.Everything));
        }

        // ---------- scope: a user sees only their departments, and nothing that sits in no department ----------

        [Fact]
        public void A_scoped_user_sees_only_their_department_in_every_report()
        {
            var only = Only(Computing);

            var enrollment = EnrollmentStatsReport.Build([new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Registered, 4, 12), new EnrollmentStat(Business, "Business", EnrollmentStatus.Registered, 9, 27)], only, null);
            var grades = GradeDistributionReport.Build([new GradeStat(Computing, "Computing", "A", 4, 380m), new GradeStat(Business, "Business", "A", 9, 810m)], only);
            var gpa = GpaStatsReport.Build([new StudentGpaStat(Computing, "Computing", 18m, 6), new StudentGpaStat(Business, "Business", 12m, 6)], only);
            var attendance = AttendanceStatsReport.Build([new AttendanceStat(Computing, "Computing", "Present", 4), new AttendanceStat(Business, "Business", "Present", 9)], only);

            Assert.All(new[] { enrollment, grades, gpa, attendance }, rows => Assert.DoesNotContain(rows, r => (string?)r[0] == "Business"));
            Assert.Equal(4, enrollment[1][2]);
            Assert.Equal(4, grades[1][2]);
            Assert.Equal(1, gpa[1][1]);
            Assert.Equal(4, attendance[1][1]);
        }

        [Fact]
        public void Nothing_is_shown_to_a_user_with_no_scope_that_covers_the_data()
        {
            var nothing = ReportScope.Nothing;

            Assert.Empty(EnrollmentStatsReport.Build([new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Registered, 4, 12)], nothing, null));
            Assert.Empty(GradeDistributionReport.Build([new GradeStat(Computing, "Computing", "A", 4, 380m)], nothing));
            Assert.Empty(GpaStatsReport.Build([new StudentGpaStat(Computing, "Computing", 18m, 6)], nothing));
            Assert.Empty(AttendanceStatsReport.Build([new AttendanceStat(Computing, "Computing", "Present", 4)], nothing));
        }

        [Fact]
        public async Task The_scope_comes_from_the_users_grants_through_the_resolver()
        {
            var env = new ReportingEnv().Grant([Permissions.Reports.Read, Permissions.AcademicReports.Enrollment], new ScopeGrant(ScopeType.Department, Computing));
            var data = new FakeAcademicReportData();
            data.Enrollments.Add(new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Registered, 4, 12));
            data.Enrollments.Add(new EnrollmentStat(Business, "Business", EnrollmentStatus.Registered, 9, 27));
            var report = new EnrollmentStatsReport(data, new ReportScopeResolver(env.Access, env.Hierarchy));

            var ran = await report.RunAsync(ReportArguments.None, ReportPage.First(50));

            Assert.True(ran.TryGetValue(out var table));
            Assert.Equal(2, table.TotalRows);
            Assert.Equal("Computing", table.Rows[0][0]);
        }

        [Fact]
        public async Task A_faculty_grant_covers_the_departments_below_it()
        {
            var faculty = Guid.NewGuid();
            var env = new ReportingEnv().Grant([Permissions.Reports.Read, Permissions.AcademicReports.Grades], new ScopeGrant(ScopeType.Faculty, faculty));
            env.Hierarchy.Chains[(ScopeType.Department, Computing)] = [new ScopeRef(ScopeType.Department, Computing), new ScopeRef(ScopeType.Faculty, faculty)];
            var data = new FakeAcademicReportData();
            data.Grades.Add(new GradeStat(Computing, "Computing", "A", 4, 380m));
            data.Grades.Add(new GradeStat(Business, "Business", "A", 9, 810m));
            var report = new GradeDistributionReport(data, new ReportScopeResolver(env.Access, env.Hierarchy));

            var ran = await report.RunAsync(ReportArguments.None, ReportPage.First(50));

            Assert.True(ran.TryGetValue(out var table));
            Assert.Equal(2, table.TotalRows);
            Assert.Equal("Computing", table.Rows[0][0]);
        }

        // ---------- filters and failures ----------

        [Fact]
        public async Task The_dates_become_whole_days_for_the_data_source()
        {
            var env = new ReportingEnv().Grant([Permissions.AcademicReports.Enrollment, Permissions.AcademicReports.Grades, Permissions.AcademicReports.Attendance]);
            var data = new FakeAcademicReportData();
            var providers = Providers(env, data);
            var arguments = Dates("2026-11-01", "2026-11-07");

            foreach (var provider in providers.Where(p => p is not GpaStatsReport))
            {
                Assert.True((await provider.RunAsync(arguments, ReportPage.First(50))).IsSuccess);
            }

            Assert.Equal((new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero)), data.EnrollmentRange);
            Assert.Equal(data.EnrollmentRange, data.GradeRange);
            Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7)), data.AttendanceRange);
        }

        [Fact]
        public async Task A_range_that_ends_before_it_starts_is_refused_before_any_query()
        {
            var env = new ReportingEnv().Grant(Permissions.AllCodes);
            var data = new FakeAcademicReportData();

            foreach (var provider in Providers(env, data))
            {
                var ran = await provider.RunAsync(Dates("2026-11-08", "2026-11-01"), ReportPage.First(50));

                ResultAssert.Failed(ran, ReportingErrors.DateRangeInvalid);
            }
        }

        [Fact]
        public async Task A_source_that_is_too_large_is_a_result_not_a_partial_report()
        {
            var env = new ReportingEnv().Grant(Permissions.AllCodes);
            var data = new FakeAcademicReportData { Fail = ReportingErrors.SourceTooLarge };

            foreach (var provider in Providers(env, data))
            {
                ResultAssert.Failed(await provider.RunAsync(ReportArguments.None, ReportPage.First(50)), ReportingErrors.SourceTooLarge);
            }
        }

        // ---------- permissions through the real handler ----------

        [Fact]
        public async Task Running_a_report_needs_Reports_Read_and_the_reports_own_permission()
        {
            var data = new FakeAcademicReportData();
            data.Enrollments.Add(new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Registered, 4, 12));
            var request = new UniCore.Application.Common.Abstractions.RequestInfo("10.0.0.1", "agent");
            var none = new Dictionary<string, string?>();
            var page = new PaginationQuery(1, 50);

            var both = new ReportingEnv().Grant([Permissions.Reports.Read, Permissions.AcademicReports.Enrollment]);
            Assert.True((await both.Handler(Providers(both, data)).RunAsync(EnrollmentStatsReport.Code, none, page, request)).IsSuccess);

            var onlyRead = new ReportingEnv().Grant([Permissions.Reports.Read]);
            Assert.True((await onlyRead.Handler(Providers(onlyRead, data)).RunAsync(EnrollmentStatsReport.Code, none, page, request)).IsFailure);

            var onlyOwn = new ReportingEnv().Grant([Permissions.AcademicReports.Enrollment]);
            Assert.True((await onlyOwn.Handler(Providers(onlyOwn, data)).RunAsync(EnrollmentStatsReport.Code, none, page, request)).IsFailure);

            // Another report's permission does not open this one.
            var otherReport = new ReportingEnv().Grant([Permissions.Reports.Read, Permissions.AcademicReports.Grades]);
            Assert.True((await otherReport.Handler(Providers(otherReport, data)).RunAsync(EnrollmentStatsReport.Code, none, page, request)).IsFailure);
        }

        [Fact]
        public async Task The_catalog_lists_only_the_academic_reports_the_user_may_run()
        {
            var env = new ReportingEnv().Grant([Permissions.Reports.Read, Permissions.AcademicReports.Gpa, Permissions.AcademicReports.Attendance]);

            var listed = await env.Handler(Providers(env, new FakeAcademicReportData())).ListAsync();

            Assert.True(listed.TryGetValue(out var reports));
            Assert.Equal(new[] { AttendanceStatsReport.Code, GpaStatsReport.Code }, reports.Select(r => r.Code).Order(StringComparer.Ordinal).ToArray());
        }

        // ---------- the rules every report keeps ----------

        [Fact]
        public void Each_report_has_its_own_known_permission_a_unique_code_and_unique_columns()
        {
            var env = new ReportingEnv();
            var descriptors = Providers(env, new FakeAcademicReportData()).Select(p => p.Descriptor).ToList();

            Assert.Equal(4, descriptors.Select(d => d.Permission).Distinct().Count());
            Assert.All(descriptors, d =>
            {
                Assert.Matches("^[A-Z_]+$", d.Code);
                Assert.Contains(d.Permission, Permissions.AllCodes);
                Assert.NotEqual(Permissions.Reports.Read, d.Permission);
                Assert.False(d.IsSensitive);
                Assert.Equal(d.Columns.Count, d.Columns.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count());
                Assert.Contains(d.Parameters, p => p.Name == "from");
                Assert.Contains(d.Parameters, p => p.Name == "to");
            });
        }

        [Fact]
        public async Task Every_row_is_as_wide_as_the_columns()
        {
            var env = new ReportingEnv().Grant(Permissions.AllCodes);
            var data = new FakeAcademicReportData();
            data.Enrollments.Add(new EnrollmentStat(Computing, "Computing", EnrollmentStatus.Registered, 4, 12));
            data.Grades.Add(new GradeStat(Computing, "Computing", "A", 4, 380m));
            data.Gpas.Add(new StudentGpaStat(Computing, "Computing", 18m, 6));
            data.Attendance.Add(new AttendanceStat(Computing, "Computing", "Present", 4));

            foreach (var provider in Providers(env, data))
            {
                var ran = await provider.RunAsync(ReportArguments.None, ReportPage.First(50));

                Assert.True(ran.TryGetValue(out var table), provider.Descriptor.Code);
                Assert.NotEmpty(table.Rows);
                Assert.All(table.Rows, row => Assert.Equal(provider.Descriptor.Columns.Count, row.Count));
            }
        }

        [Fact]
        public void The_registrar_and_the_department_head_can_run_all_four_reports_and_students_none()
        {
            string[] reports = [Permissions.AcademicReports.Enrollment, Permissions.AcademicReports.Grades, Permissions.AcademicReports.Gpa, Permissions.AcademicReports.Attendance];

            foreach (var role in new[] { RoleNames.Registrar, RoleNames.DepartmentHead, RoleNames.Dean })
            {
                var held = DefaultRolePermissions.ByRole[role];
                Assert.Contains(Permissions.Reports.Read, held);
                Assert.All(reports, p => Assert.Contains(p, held));
            }

            Assert.All(reports, p => Assert.DoesNotContain(p, DefaultRolePermissions.ByRole[RoleNames.Student]));
        }
    }
}
