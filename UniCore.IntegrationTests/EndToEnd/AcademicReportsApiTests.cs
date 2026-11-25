using System.Text.Json;
using UniCore.Application.Modules.Reporting.Reports;
using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // The four academic reports through the API. Every run uses a user scoped to the scenario's own department, so
    // the numbers are those of this test only (the database is shared by all tests) and the scope rule is exercised
    // at the same time.
    [Collection(ApiCollection.Name)]
    public sealed class AcademicReportsApiTests(ApiFixture fixture)
    {
        private static readonly (string Code, string Permission)[] Reports =
        [
            (EnrollmentStatsReport.Code, Permissions.AcademicReports.Enrollment),
            (GradeDistributionReport.Code, Permissions.AcademicReports.Grades),
            (GpaStatsReport.Code, Permissions.AcademicReports.Gpa),
            (AttendanceStatsReport.Code, Permissions.AcademicReports.Attendance)
        ];

        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        private Task<string> ReaderAsync(string permission, Guid departmentId)
            => RoleUsers.TokenAsync(_api, "acadrep", [Permissions.Reports.Read, permission], ScopeType.Department, departmentId);

        private Task<ApiResponse> RunAsync(string token, string code, string query = "")
            => Client.GetAsync($"/api/v1/reports/{code}/run{query}", token);

        private static JsonElement Row(ApiResponse run, int index) => run.Prop("rows")[index];

        private static int RowCount(ApiResponse run) => run.Prop("rows").GetArrayLength();

        private async Task PublishAsync(string admin, GradingScenario scenario)
        {
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);
        }

        // ---------- who may run them ----------

        [Fact]
        public async Task Each_report_needs_Reports_Read_and_its_own_permission()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var department = scenario.Setup.DepartmentId;
            var student = await _api.CreateUserAsync("acadstudent", RoleNames.Student);
            var studentToken = await _api.LoginTokenAsync(student.UserName, student.Password);

            foreach (var (code, permission) in Reports)
            {
                var both = await ReaderAsync(permission, department);
                var onlyRead = await RoleUsers.TokenAsync(_api, "acadread", [Permissions.Reports.Read], ScopeType.Department, department);
                var onlyOwn = await RoleUsers.TokenAsync(_api, "acadown", [permission], ScopeType.Department, department);

                Assert.Equal(200, (await RunAsync(both, code)).Code);
                Assert.Equal(403, (await RunAsync(onlyRead, code)).Code);
                Assert.Equal(403, (await RunAsync(onlyOwn, code)).Code);
                Assert.Equal(403, (await RunAsync(studentToken, code)).Code);
                Assert.Equal(401, (await Client.GetAsync($"/api/v1/reports/{code}/run", null)).Code);
            }
        }

        [Fact]
        public async Task The_catalog_shows_each_user_only_the_academic_reports_they_hold()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var gpaOnly = await ReaderAsync(Permissions.AcademicReports.Gpa, scenario.Setup.DepartmentId);

            var listed = await Client.GetAsync("/api/v1/reports", gpaOnly);

            Assert.Equal(200, listed.Code);
            var codes = Enumerable.Range(0, listed.Body.GetArrayLength()).Select(i => listed.Body[i].GetProperty("code").GetString()).ToList();
            Assert.Equal([GpaStatsReport.Code], codes);
        }

        // ---------- enrollment statistics ----------

        [Fact]
        public async Task Enrollment_statistics_count_the_seats_taken_in_the_users_department_only()
        {
            var admin = await _api.AdminTokenAsync();
            var mine = await GradingScenario.BuildAsync(_api, admin, students: 3);
            var other = await GradingScenario.BuildAsync(_api, admin, students: 2);

            var run = await RunAsync(await ReaderAsync(Permissions.AcademicReports.Enrollment, mine.Setup.DepartmentId), EnrollmentStatsReport.Code);

            Assert.Equal(200, run.Code);
            Assert.Equal(2, RowCount(run));
            Assert.Equal("Registered", Row(run, 0)[1].GetString());
            Assert.Equal(3, Row(run, 0)[2].GetInt32());
            Assert.Equal(9, Row(run, 0)[3].GetInt32());
            Assert.Equal("Total", Row(run, 1)[0].GetString());
            Assert.Equal(3, Row(run, 1)[2].GetInt32());

            // The other department's user sees its own two students, not five.
            var theirs = await RunAsync(await ReaderAsync(Permissions.AcademicReports.Enrollment, other.Setup.DepartmentId), EnrollmentStatsReport.Code);
            Assert.Equal(2, Row(theirs, 0)[2].GetInt32());
        }

        [Fact]
        public async Task Enrollment_statistics_follow_the_date_and_status_filters()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2);
            var token = await ReaderAsync(Permissions.AcademicReports.Enrollment, scenario.Setup.DepartmentId);
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var lastYear = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd");

            var inRange = await RunAsync(token, EnrollmentStatsReport.Code, $"?from={today}&to={today}");
            var before = await RunAsync(token, EnrollmentStatsReport.Code, $"?to={lastYear}");
            var dropped = await RunAsync(token, EnrollmentStatsReport.Code, "?status=dropped");
            var backwards = await RunAsync(token, EnrollmentStatsReport.Code, $"?from={today}&to={lastYear}");

            Assert.Equal(2, Row(inRange, 0)[2].GetInt32());
            Assert.Equal(0, RowCount(before));
            Assert.Equal(0, RowCount(dropped));
            Assert.Equal("REPORT_DATE_RANGE_INVALID", backwards.ErrorCode);
        }

        // ---------- grade distribution and GPA ----------

        [Fact]
        public async Task The_grade_distribution_shows_published_grades_only()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 3);
            var token = await ReaderAsync(Permissions.AcademicReports.Grades, scenario.Setup.DepartmentId);

            // Draft grades are not in the report.
            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(0, RowCount(await RunAsync(token, GradeDistributionReport.Code)));

            await PublishAsync(admin, scenario);

            var run = await RunAsync(token, GradeDistributionReport.Code);

            Assert.Equal(200, run.Code);
            Assert.Equal(2, RowCount(run));
            Assert.Equal(3, Row(run, 0)[2].GetInt32());
            Assert.Equal(100m, Row(run, 0)[3].GetDecimal());
            Assert.Equal(70m, Row(run, 0)[4].GetDecimal());
            Assert.Equal("Total", Row(run, 1)[0].GetString());
            Assert.Equal(3, Row(run, 1)[2].GetInt32());
        }

        [Fact]
        public async Task The_grade_distribution_date_filter_uses_the_publication_day()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2);
            await PublishAsync(admin, scenario);
            var token = await ReaderAsync(Permissions.AcademicReports.Grades, scenario.Setup.DepartmentId);
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var lastYear = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd");

            Assert.Equal(2, Row(await RunAsync(token, GradeDistributionReport.Code, $"?from={today}&to={today}"), 0)[2].GetInt32());
            Assert.Equal(0, RowCount(await RunAsync(token, GradeDistributionReport.Code, $"?to={lastYear}")));
        }

        [Fact]
        public async Task The_gpa_statistics_count_every_student_with_a_published_grade()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 3);
            var token = await ReaderAsync(Permissions.AcademicReports.Gpa, scenario.Setup.DepartmentId);

            Assert.Equal(0, RowCount(await RunAsync(token, GpaStatsReport.Code)));

            await PublishAsync(admin, scenario);

            var run = await RunAsync(token, GpaStatsReport.Code);

            Assert.Equal(200, run.Code);
            Assert.Equal(2, RowCount(run));
            var department = Row(run, 0);
            Assert.Equal(3, department[1].GetInt32());

            // The three students have the same grade: average, lowest and highest agree, and all sit in one band.
            Assert.Equal(department[2].GetDecimal(), department[3].GetDecimal());
            Assert.Equal(department[2].GetDecimal(), department[4].GetDecimal());
            Assert.Equal(3, Enumerable.Range(5, 4).Sum(i => department[i].GetInt32()));
            Assert.Equal(3, Enumerable.Range(5, 4).Select(i => department[i].GetInt32()).Max());
            Assert.Equal("Total", Row(run, 1)[0].GetString());
            Assert.Equal(3, Row(run, 1)[1].GetInt32());
        }

        // ---------- attendance ----------

        [Fact]
        public async Task Attendance_statistics_count_closed_sessions_only()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 3);
            var token = await ReaderAsync(Permissions.AcademicReports.Attendance, scenario.Setup.DepartmentId);
            var students = scenario.Students;

            async Task<Guid> OpenAsync(int daysAgo, params string[] statuses)
            {
                var session = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = scenario.SectionId, sessionDate = Today.AddDays(-daysAgo), startTime = "09:00:00" }, admin);
                Assert.Equal(201, session.Code);
                var id = session.GuidOf("id");
                var marks = statuses.Select((status, i) => new { studentId = students[i].Id, status, minutesLate = status == "Late" ? 3 : 0 }).ToArray();
                Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{id}/marks", new { marks }, admin)).Code);

                return id;
            }

            var closed = await OpenAsync(5, "Present", "Absent", "Late");
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{closed}/close", null, admin)).Code);

            // An open session is not final and must not count.
            await OpenAsync(4, "Absent", "Absent", "Absent");

            var run = await RunAsync(token, AttendanceStatsReport.Code);

            Assert.Equal(200, run.Code);
            Assert.Equal(2, RowCount(run));
            var row = Row(run, 0);
            Assert.Equal(3, row[1].GetInt32());
            Assert.Equal(1, row[2].GetInt32());
            Assert.Equal(1, row[3].GetInt32());
            Assert.Equal(0, row[4].GetInt32());
            Assert.Equal(1, row[5].GetInt32());
            Assert.Equal(66.67m, row[6].GetDecimal());
            Assert.Equal("Total", Row(run, 1)[0].GetString());
            Assert.Equal(3, Row(run, 1)[1].GetInt32());

            // The date filter is the day of the session.
            var onTheDay = await RunAsync(token, AttendanceStatsReport.Code, $"?from={Today.AddDays(-5):yyyy-MM-dd}&to={Today.AddDays(-5):yyyy-MM-dd}");
            var earlier = await RunAsync(token, AttendanceStatsReport.Code, $"?to={Today.AddDays(-30):yyyy-MM-dd}");
            Assert.Equal(3, Row(onTheDay, 0)[1].GetInt32());
            Assert.Equal(0, RowCount(earlier));
        }

        [Fact]
        public async Task A_user_of_another_department_sees_none_of_this_departments_attendance()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var elsewhere = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var session = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = scenario.SectionId, sessionDate = Today.AddDays(-5), startTime = "09:00:00" }, admin);
            Assert.Equal(201, session.Code);
            var id = session.GuidOf("id");
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{id}/marks", new { marks = new[] { new { studentId = scenario.Students[0].Id, status = "Present" } } }, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{id}/close", null, admin)).Code);

            var run = await RunAsync(await ReaderAsync(Permissions.AcademicReports.Attendance, elsewhere.Setup.DepartmentId), AttendanceStatsReport.Code);

            Assert.Equal(200, run.Code);
            Assert.Equal(0, RowCount(run));
        }
    }
}
