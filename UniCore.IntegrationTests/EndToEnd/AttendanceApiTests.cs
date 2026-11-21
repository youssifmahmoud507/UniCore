using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Sessions, marks, the percentage and the exam block it can cause, and excuses.
    [Collection(ApiCollection.Name)]
    public sealed class AttendanceApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;
        private readonly RawHttp _raw = new(fixture.CreateHttpClient());

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        private static byte[] Pdf() => [.. "%PDF-1.4\n"u8.ToArray(), .. Enumerable.Repeat((byte)'x', 200)];

        private async Task<Guid> OpenAsync(string token, Guid sectionId, int daysAgo, string time = "09:00:00")
        {
            var session = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = sectionId, sessionDate = Today.AddDays(-daysAgo), startTime = time }, token);
            Assert.Equal(201, session.Code);

            return session.GuidOf("id");
        }

        private Task<ApiResponse> MarkAsync(string token, Guid sessionId, params (Guid Student, string Status)[] marks)
            => Client.PostAsync($"/api/v1/attendance/sessions/{sessionId}/marks", new { marks = marks.Select(m => new { studentId = m.Student, status = m.Status }).ToArray() }, token);

        private Task<ApiResponse> CloseAsync(string token, Guid sessionId)
            => Client.PostAsync($"/api/v1/attendance/sessions/{sessionId}/close", null, token);

        // Opens, marks every student with the given status and closes one session.
        private async Task<ApiResponse> HoldAsync(string token, GradingScenario scenario, int daysAgo, params string[] statuses)
        {
            var session = await OpenAsync(token, scenario.SectionId, daysAgo);
            var marked = await MarkAsync(token, session, statuses.Select((status, i) => (scenario.Students[i].Id, status)).ToArray());
            Assert.Equal(200, marked.Code);
            Assert.Equal(200, (await CloseAsync(token, session)).Code);

            return marked;
        }

        [Fact]
        public async Task A_session_is_opened_once_per_date_and_start_time_and_only_inside_the_semester()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);

            await OpenAsync(admin, scenario.SectionId, daysAgo: 5);

            var duplicate = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = scenario.SectionId, sessionDate = Today.AddDays(-5), startTime = "09:00:00" }, admin);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("ATTENDANCE_SESSION_EXISTS", duplicate.ErrorCode);

            var outside = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = scenario.SectionId, sessionDate = Today.AddDays(-400), startTime = "09:00:00" }, admin);
            Assert.Equal(422, outside.Code);
            Assert.Equal("ATTENDANCE_SESSION_DATE_OUTSIDE_SEMESTER", outside.ErrorCode);
        }

        [Fact]
        public async Task Marks_are_taken_for_enrolled_students_only_and_stop_when_the_session_closes()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2);
            var session = await OpenAsync(admin, scenario.SectionId, daysAgo: 6);

            var none = await Client.PostAsync($"/api/v1/attendance/sessions/{session}/marks", new { marks = Array.Empty<object>() }, admin);
            Assert.Equal(400, none.Code);
            Assert.Equal("ATTENDANCE_MARKS_REQUIRED", none.ErrorCode);

            var stranger = await MarkAsync(admin, session, (Guid.NewGuid(), "Present"));
            Assert.Equal(422, stranger.Code);
            Assert.Equal("ATTENDANCE_STUDENT_NOT_ENROLLED", stranger.ErrorCode);

            var marked = await MarkAsync(admin, session, (scenario.Students[0].Id, "Present"), (scenario.Students[1].Id, "Absent"));
            Assert.Equal(200, marked.Code);
            Assert.Equal(2, marked.Prop("records").GetArrayLength());

            // A mark can be corrected while the session is open.
            var corrected = await MarkAsync(admin, session, (scenario.Students[1].Id, "Present"));
            Assert.Equal(200, corrected.Code);

            var closed = await CloseAsync(admin, session);
            Assert.Equal(200, closed.Code);
            Assert.Equal("Closed", closed.Prop("status").GetString());

            var late = await MarkAsync(admin, session, (scenario.Students[0].Id, "Absent"));
            Assert.Equal(422, late.Code);
            Assert.Equal("ATTENDANCE_SESSION_CLOSED", late.ErrorCode);

            var again = await CloseAsync(admin, session);
            Assert.Equal(422, again.Code);
            Assert.Equal("ATTENDANCE_SESSION_NOT_OPEN", again.ErrorCode);
        }

        [Fact]
        public async Task Only_closed_sessions_count_and_a_low_percentage_blocks_the_exam_under_the_policy()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var student = scenario.Students[0].Id;
            var course = scenario.Setup.Courses[0].CourseId;

            var policy = await Client.PostAsync("/api/v1/attendance/policies", new { courseId = course, minimumPercentage = 80m, warningPercentage = 90m, lateThresholdMinutes = 10, isExamBlocking = true }, admin);
            Assert.Equal(201, policy.Code);

            var twice = await Client.PostAsync("/api/v1/attendance/policies", new { courseId = course, minimumPercentage = 70m, warningPercentage = 80m, lateThresholdMinutes = 10, isExamBlocking = false }, admin);
            Assert.Equal(409, twice.Code);
            Assert.Equal("ATTENDANCE_POLICY_EXISTS", twice.ErrorCode);

            await HoldAsync(admin, scenario, 10, "Present");
            await HoldAsync(admin, scenario, 9, "Present");
            await HoldAsync(admin, scenario, 8, "Present");
            await HoldAsync(admin, scenario, 7, "Absent");

            // A session that is still open does not count.
            var open = await OpenAsync(admin, scenario.SectionId, daysAgo: 6);
            Assert.Equal(200, (await MarkAsync(admin, open, (student, "Absent"))).Code);

            var report = await Client.GetAsync($"/api/v1/attendance/reports/students/{student}/sections/{scenario.SectionId}", admin);
            Assert.Equal(200, report.Code);
            Assert.Equal(4, report.Prop("closedSessions").GetInt32());
            Assert.Equal(3, report.Prop("attended").GetInt32());
            Assert.Equal(75m, report.Prop("percentage").GetDecimal());
            Assert.Equal("Critical", report.Prop("level").GetString());
            Assert.True(report.Prop("isExamBlocked").GetBoolean());
        }

        [Fact]
        public async Task A_late_arrival_within_the_threshold_counts_as_attended()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var student = scenario.Students[0].Id;

            // Without a policy the late threshold is zero, so a policy of 10 minutes decides what counts.
            var policy = await Client.PostAsync("/api/v1/attendance/policies", new { courseId = scenario.Setup.Courses[0].CourseId, minimumPercentage = 75m, warningPercentage = 85m, lateThresholdMinutes = 10, isExamBlocking = false }, admin);
            Assert.Equal(201, policy.Code);

            var session = await OpenAsync(admin, scenario.SectionId, daysAgo: 12);
            var marked = await Client.PostAsync($"/api/v1/attendance/sessions/{session}/marks", new { marks = new[] { new { studentId = student, status = "Late", minutesLate = 3 } } }, admin);
            Assert.Equal(200, marked.Code);
            Assert.Equal(200, (await CloseAsync(admin, session)).Code);

            var report = await Client.GetAsync($"/api/v1/attendance/reports/students/{student}/sections/{scenario.SectionId}", admin);

            Assert.Equal(200, report.Code);
            Assert.Equal(100m, report.Prop("percentage").GetDecimal());
        }

        [Fact]
        public async Task A_student_excuses_their_own_absence_and_an_accepted_excuse_stops_it_counting()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2, createAccounts: true);
            var absent = scenario.Students[0];
            var present = scenario.Students[1];

            var marked = await HoldAsync(admin, scenario, 11, "Absent", "Present");
            var absentRecord = marked.Prop("records").EnumerateArray().Single(r => r.GetProperty("studentId").GetGuid() == absent.Id).GetProperty("id").GetGuid();
            var presentRecord = marked.Prop("records").EnumerateArray().Single(r => r.GetProperty("studentId").GetGuid() == present.Id).GetProperty("id").GetGuid();

            var token = await _api.LoginTokenAsync(absent.Number, TestApi.DefaultPassword);

            // Only an absence can be excused.
            var notAbsence = await _raw.PostFileAsync("/api/v1/attendance/excuses", token, "note.pdf", Pdf(), "application/pdf", new Dictionary<string, string> { ["attendanceRecordId"] = presentRecord.ToString(), ["reason"] = "Not needed" });
            Assert.True(notAbsence.Code is 403 or 422, $"unexpected {notAbsence.Code}");

            var submitted = await _raw.PostFileAsync("/api/v1/attendance/excuses", token, "note.pdf", Pdf(), "application/pdf", new Dictionary<string, string> { ["attendanceRecordId"] = absentRecord.ToString(), ["reason"] = "Hospital visit" });
            Assert.Equal(201, submitted.Code);
            Assert.Equal("Pending", submitted.Str("status"));
            var excuseId = submitted.Prop("id").GetGuid();

            var duplicate = await _raw.PostFileAsync("/api/v1/attendance/excuses", token, "note.pdf", Pdf(), "application/pdf", new Dictionary<string, string> { ["attendanceRecordId"] = absentRecord.ToString(), ["reason"] = "Again" });
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("ATTENDANCE_EXCUSE_EXISTS", duplicate.ErrorCode);

            // The other student cannot excuse someone else's absence.
            var otherToken = await _api.LoginTokenAsync(present.Number, TestApi.DefaultPassword);
            var theirs = await _raw.PostFileAsync("/api/v1/attendance/excuses", otherToken, "note.pdf", Pdf(), "application/pdf", new Dictionary<string, string> { ["attendanceRecordId"] = absentRecord.ToString(), ["reason"] = "Not mine" });
            Assert.Equal(403, theirs.Code);

            // Staff review: a rejection needs a note, an acceptance turns the absence into an excused one.
            var noNote = await Client.PostAsync($"/api/v1/attendance/excuses/{excuseId}/review", new { accept = false }, admin);
            Assert.Equal(400, noNote.Code);
            Assert.Equal("ATTENDANCE_EXCUSE_NOTE_REQUIRED", noNote.ErrorCode);

            var before = await Client.GetAsync($"/api/v1/attendance/reports/students/{absent.Id}/sections/{scenario.SectionId}", admin);
            Assert.Equal(0m, before.Prop("percentage").GetDecimal());

            var accepted = await Client.PostAsync($"/api/v1/attendance/excuses/{excuseId}/review", new { accept = true, note = "Certificate checked" }, admin);
            Assert.Equal(200, accepted.Code);
            Assert.Equal("Accepted", accepted.Prop("status").GetString());

            var after = await Client.GetAsync($"/api/v1/attendance/reports/students/{absent.Id}/sections/{scenario.SectionId}", admin);
            Assert.Equal(100m, after.Prop("percentage").GetDecimal());
            Assert.Equal(1, after.Prop("excused").GetInt32());

            var again = await Client.PostAsync($"/api/v1/attendance/excuses/{excuseId}/review", new { accept = true, note = "Twice" }, admin);
            Assert.Equal(422, again.Code);
            Assert.Equal("ATTENDANCE_EXCUSE_NOT_PENDING", again.ErrorCode);
        }

        [Fact]
        public async Task A_student_cannot_open_a_session_or_mark_attendance()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1, createAccounts: true);
            var token = await _api.LoginTokenAsync(scenario.Students[0].Number, TestApi.DefaultPassword);

            var open = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = scenario.SectionId, sessionDate = Today.AddDays(-3), startTime = "09:00:00" }, token);
            Assert.Equal(403, open.Code);

            var session = await OpenAsync(admin, scenario.SectionId, daysAgo: 4);
            var mark = await MarkAsync(token, session, (scenario.Students[0].Id, "Present"));
            Assert.Equal(403, mark.Code);
        }
    }
}
