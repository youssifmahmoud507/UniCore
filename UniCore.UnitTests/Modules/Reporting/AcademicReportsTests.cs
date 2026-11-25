using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Dashboard;
using UniCore.Application.Modules.Reporting.Reports;
using UniCore.Domain.Modules.Students;

namespace UniCore.UnitTests.Modules.Reporting
{
    public class AcademicReportsTests
    {
        private static readonly Guid Engineering = Guid.NewGuid();
        private static readonly Guid Medicine = Guid.NewGuid();

        private static ReportScope Only(Guid department) => new(false, new HashSet<Guid> { department });

        [Fact]
        public void Enrollment_by_section_shows_seats_left_and_how_full_each_section_is_with_a_total()
        {
            var semester = Guid.NewGuid();
            var rows = EnrollmentBySectionReport.Build(
            [
                new SectionEnrollmentFact(Engineering, semester, "CS102", "Data", "B", 40, 10),
                new SectionEnrollmentFact(Engineering, semester, "CS101", "Intro", "A", 30, 30),
                new SectionEnrollmentFact(Engineering, semester, "CS101", "Intro", "B", 30, 15)
            ], ReportScope.Everything);

            Assert.Equal(4, rows.Count);
            Assert.Equal(["CS101", "Intro", "A", 30, 30, 0, 100m], rows[0]);
            Assert.Equal(["CS101", "Intro", "B", 30, 15, 15, 50m], rows[1]);
            Assert.Equal(["CS102", "Data", "B", 40, 10, 30, 25m], rows[2]);
            Assert.Equal([EnrollmentBySectionReport.TotalLabel, null, null, 100, 55, 45, 55m], rows[3]);
        }

        [Fact]
        public void Enrollment_by_section_only_shows_the_sections_in_scope_and_a_section_without_seats_is_zero_percent_full()
        {
            var semester = Guid.NewGuid();
            var rows = EnrollmentBySectionReport.Build(
            [
                new SectionEnrollmentFact(Engineering, semester, "CS101", "Intro", "A", 0, 0),
                new SectionEnrollmentFact(Medicine, semester, "MD101", "Anatomy", "A", 20, 20)
            ], Only(Engineering));

            Assert.Equal(2, rows.Count);
            Assert.Equal("CS101", rows[0][0]);
            Assert.Equal(0m, rows[0][6]);
            Assert.Empty(EnrollmentBySectionReport.Build([new SectionEnrollmentFact(Medicine, semester, "MD101", "Anatomy", "A", 20, 20)], Only(Engineering)));
        }

        [Fact]
        public async Task Enrollment_by_section_refuses_a_semester_id_that_is_not_an_id()
        {
            var env = new ReportingEnv().Grant(UniCore.Domain.Modules.Identity.Permissions.AllCodes);
            var report = new EnrollmentBySectionReport(env.Data, new ReportScopeResolver(env.Access, env.Hierarchy));

            var ran = await report.RunAsync(new ReportArguments(new Dictionary<string, object> { ["semesterId"] = "nope" }), ReportPage.First(10));

            Assert.True(ran.IsFailure);
            Assert.Equal("REPORT_PARAMETER_INVALID", ran.Error?.Code);

            var semester = Guid.NewGuid();
            Assert.True((await report.RunAsync(new ReportArguments(new Dictionary<string, object> { ["semesterId"] = semester.ToString() }), ReportPage.First(10))).IsSuccess);
            Assert.Equal(semester, env.Data.SectionSemester);
        }

        [Fact]
        public void Students_by_status_adds_the_departments_up_and_keeps_students_without_a_program_for_global_users()
        {
            StudentStatusStat[] stats =
            [
                new(StudentAcademicStatus.Active, Engineering, 10),
                new(StudentAcademicStatus.Active, Medicine, 4),
                new(StudentAcademicStatus.OnLeave, Engineering, 2),
                new(StudentAcademicStatus.Active, null, 1)
            ];

            var everything = StudentsByStatusReport.Build(stats, ReportScope.Everything);
            Assert.Equal(["Active", 15], everything[0]);
            Assert.Equal(["OnLeave", 2], everything[1]);
            Assert.Equal([StudentsByStatusReport.TotalLabel, 17], everything[2]);

            var engineering = StudentsByStatusReport.Build(stats, Only(Engineering));
            Assert.Equal(["Active", 10], engineering[0]);
            Assert.Equal([StudentsByStatusReport.TotalLabel, 12], engineering[2]);
        }

        [Fact]
        public void The_academic_widget_counts_students_and_waiting_requests_in_scope()
        {
            StudentStatusStat[] students = [new(StudentAcademicStatus.Active, Engineering, 10), new(StudentAcademicStatus.OnLeave, Engineering, 2), new(StudentAcademicStatus.Active, Medicine, 7)];
            AcademicRequestStat[] pending = [new("Withdrawal", Engineering, 3), new("Leave", Engineering, 1), new("Withdrawal", Medicine, 9)];

            var metrics = AcademicDashboardWidget.Build(students, pending, Only(Engineering)).ToDictionary(m => m.Key, m => m.Value);

            Assert.Equal(12m, metrics["students"]);
            Assert.Equal(10m, metrics["active"]);
            Assert.Equal(2m, metrics["onLeave"]);
            Assert.Equal(3m, metrics["pendingWithdrawals"]);
            Assert.Equal(1m, metrics["pendingLeaves"]);
        }
    }
}
