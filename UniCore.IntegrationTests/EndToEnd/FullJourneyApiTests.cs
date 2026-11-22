using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Whole journeys that cross modules, each from the first record to the last check. They are written to find the
    // bugs that live BETWEEN modules (a rule in one place that another place does not respect), so every step builds
    // on the real result of the step before and nothing is mocked except what the test factory already replaces.
    [Collection(ApiCollection.Name)]
    public sealed class FullJourneyApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;
        private readonly ApiFixture _fixture = fixture;
        private readonly RawHttp _raw = new(fixture.CreateHttpClient());

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        private static string Short(int length = 8) => Guid.NewGuid().ToString("N")[..length].ToUpperInvariant();

        private static byte[] Pdf() => [.. "%PDF-1.4\n"u8.ToArray(), .. Enumerable.Repeat((byte)'x', 200)];

        private static Dictionary<string, string> Key() => new() { ["Idempotency-Key"] = $"key-{Guid.NewGuid():N}" };

        // Opens a session, marks every student in order with the given statuses and closes it. Returns the marks response.
        private async Task<ApiResponse> HoldSessionAsync(string token, GradingScenario scenario, int daysAgo, params string[] statuses)
        {
            var session = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = scenario.SectionId, sessionDate = Today.AddDays(-daysAgo), startTime = "09:00:00" }, token);
            Assert.Equal(201, session.Code);
            var id = session.GuidOf("id");

            var marked = await Client.PostAsync($"/api/v1/attendance/sessions/{id}/marks", new { marks = statuses.Select((status, i) => new { studentId = scenario.Students[i].Id, status }).ToArray() }, token);
            Assert.Equal(200, marked.Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{id}/close", null, token)).Code);

            return marked;
        }

        private async Task<Guid> OpenAttendanceAsync(string token, Guid sectionId, int daysAgo)
        {
            var session = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = sectionId, sessionDate = Today.AddDays(-daysAgo), startTime = "09:00:00" }, token);
            Assert.Equal(201, session.Code);

            return session.GuidOf("id");
        }

        // ------------------------------------------------------------------------------------------------------------
        // Journey 1: one section, three very different students, all the way from registration to the official transcript.
        // ------------------------------------------------------------------------------------------------------------
        [Fact]
        public async Task Journey_one_section_three_students_from_registration_to_the_official_transcript()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 3, createAccounts: true);
            var course = scenario.Setup.Courses[0].CourseId;
            var excused = scenario.Students[0];
            var failing = scenario.Students[1];
            var top = scenario.Students[2];

            // 1. Each student can see the registration the registrar made for them.
            foreach (var student in scenario.Students)
            {
                var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
                var mine = await Client.GetAsync($"/api/v1/registrations/me?semesterId={scenario.Setup.Calendar.SemesterId}", token);
                Assert.Equal(200, mine.Code);
            }

            // 2. Attendance: a policy that blocks the exam under 80 percent, then four closed sessions.
            var policy = await Client.PostAsync("/api/v1/attendance/policies", new { courseId = course, minimumPercentage = 80m, warningPercentage = 90m, lateThresholdMinutes = 10, isExamBlocking = true }, admin);
            Assert.Equal(201, policy.Code);

            var first = await HoldSessionAsync(admin, scenario, 40, "Absent", "Absent", "Present");
            await HoldSessionAsync(admin, scenario, 39, "Present", "Absent", "Present");
            await HoldSessionAsync(admin, scenario, 38, "Present", "Absent", "Present");
            await HoldSessionAsync(admin, scenario, 37, "Present", "Present", "Present");

            async Task<ApiResponse> ReportAsync(Guid student)
                => await Client.GetAsync($"/api/v1/attendance/reports/students/{student}/sections/{scenario.SectionId}", admin);

            var before = await ReportAsync(excused.Id);
            Assert.Equal(75m, before.Prop("percentage").GetDecimal());

            var failingReport = await ReportAsync(failing.Id);
            Assert.Equal(25m, failingReport.Prop("percentage").GetDecimal());
            Assert.True(failingReport.Prop("isExamBlocked").GetBoolean());

            Assert.Equal(100m, (await ReportAsync(top.Id)).Prop("percentage").GetDecimal());
            Assert.False((await ReportAsync(top.Id)).Prop("isExamBlocked").GetBoolean());

            // 3. The student with one absence excuses it; staff accept; the percentage follows.
            var record = first.Prop("records").EnumerateArray().Single(r => r.GetProperty("studentId").GetGuid() == excused.Id).GetProperty("id").GetGuid();
            var excusedToken = await _api.LoginTokenAsync(excused.Number, TestApi.DefaultPassword);

            var excuse = await _raw.PostFileAsync("/api/v1/attendance/excuses", excusedToken, "doctor.pdf", Pdf(), "application/pdf", new Dictionary<string, string> { ["attendanceRecordId"] = record.ToString(), ["reason"] = "Hospital" });
            Assert.Equal(201, excuse.Code);

            var accepted = await Client.PostAsync($"/api/v1/attendance/excuses/{excuse.GuidOf("id")}/review", new { accept = true, note = "Certificate seen" }, admin);
            Assert.Equal(200, accepted.Code);

            Assert.Equal(100m, (await ReportAsync(excused.Id)).Prop("percentage").GetDecimal());

            // 4. The final exam: session, exam, time, a room that fits, a chief invigilator, published.
            var examSession = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = scenario.Setup.Calendar.SemesterId, name = $"Journey finals {Short(8)}", examType = "Final", startDate = Today.AddDays(-20), endDate = Today.AddDays(-10) }, admin);
            Assert.Equal(201, examSession.Code);

            var exam = await Client.PostAsync("/api/v1/exams", new { courseSectionId = scenario.SectionId, examSessionId = examSession.GuidOf("id"), examType = "Final", maxScore = 100m, durationMinutes = 120 }, admin);
            Assert.Equal(201, exam.Code);
            var examId = exam.GuidOf("id");

            Assert.Equal(200, (await Client.PutAsync($"/api/v1/exams/{examId}/schedule", new { examDate = Today.AddDays(-15), startTime = "09:00", endTime = "12:00" }, admin)).Code);

            var room = await RoomSeed.RoomAsync(_fixture, _api, admin, capacity: 40);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/exams/{examId}/rooms", new { roomId = room }, admin)).Code);

            var invigilator = await AcademicScenario.CreateInstructorAsync(_api, admin, scenario.Setup.DepartmentId);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/exams/{examId}/invigilators", new { roomId = room, instructorId = invigilator, role = "Chief" }, admin)).Code);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/exams/{examId}/publish", null, admin)).Code);

            // 5. The roster is where attendance and the exam meet: the blocked student is left out.
            var roster = await Client.GetAsync($"/api/v1/exams/{examId}/roster", admin);
            Assert.Equal(200, roster.Code);
            Assert.Equal(new HashSet<Guid> { excused.Id, top.Id }, roster.Prop("eligibleStudentIds").EnumerateArray().Select(e => e.GetGuid()).ToHashSet());
            Assert.Equal(new HashSet<Guid> { failing.Id }, roster.Prop("blockedStudentIds").EnumerateArray().Select(e => e.GetGuid()).ToHashSet());

            // 6. Grades: 84 percent, 20 percent and 100 percent, through the whole chain.
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, excused.Id, quiz: 18m, final: 80m)).Code);
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, failing.Id, quiz: 4m, final: 20m)).Code);
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, top.Id, quiz: 20m, final: 100m)).Code);

            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);

            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);

            // Until the grades are published a student sees nothing of them.
            var hidden = await Client.GetAsync($"/api/v1/grades/students/{top.Id}", await _api.LoginTokenAsync(top.Number, TestApi.DefaultPassword));
            Assert.Equal(200, hidden.Code);
            Assert.DoesNotContain(hidden.Body.EnumerateArray(), g => g.GetProperty("status").GetString() is "Draft" or "Submitted" or "Reviewed" or "Approved");

            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            // 7. The transcript sees the published grades: the failing course is attempted but earns nothing.
            async Task<System.Text.Json.JsonElement> GpaAsync(Guid student)
            {
                var gpa = await Client.GetAsync($"/api/v1/transcripts/students/{student}/gpa", admin);
                Assert.Equal(200, gpa.Code);
                return gpa.Prop("cumulative");
            }

            var excusedGpa = await GpaAsync(excused.Id);
            var failingGpa = await GpaAsync(failing.Id);
            var topGpa = await GpaAsync(top.Id);

            Assert.Equal(3, excusedGpa.GetProperty("earnedCredits").GetInt32());
            Assert.Equal(3, failingGpa.GetProperty("attemptedCredits").GetInt32());
            Assert.Equal(0, failingGpa.GetProperty("earnedCredits").GetInt32());
            Assert.Equal(3, topGpa.GetProperty("earnedCredits").GetInt32());

            Assert.True(topGpa.GetProperty("gpa").GetDecimal() >= excusedGpa.GetProperty("gpa").GetDecimal());
            Assert.True(excusedGpa.GetProperty("gpa").GetDecimal() > failingGpa.GetProperty("gpa").GetDecimal());

            // 8. The registrar issues an official transcript for the top student, who can read it.
            var issued = await Client.PostAsync($"/api/v1/transcripts/students/{top.Id}/snapshots", null, admin);
            Assert.Equal(200, issued.Code);

            var topToken = await _api.LoginTokenAsync(top.Number, TestApi.DefaultPassword);
            var snapshot = await Client.GetAsync($"/api/v1/transcripts/snapshots/{issued.GuidOf("snapshotId")}", topToken);
            Assert.Equal(200, snapshot.Code);
            Assert.Equal(100m, snapshot.Prop("semesters")[0].GetProperty("courses")[0].GetProperty("percentage").GetDecimal());

            // The student whose exam was blocked cannot read the top student's snapshot.
            var failingToken = await _api.LoginTokenAsync(failing.Number, TestApi.DefaultPassword);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/transcripts/snapshots/{issued.GuidOf("snapshotId")}", failingToken)).Code);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Journey 2: money and registration. Fees for the real program and semester, a hold, a payment, a drop, a refund.
        // ------------------------------------------------------------------------------------------------------------
        [Fact]
        public async Task Journey_fees_hold_payment_drop_and_refund_around_one_registration()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            var semester = setup.Calendar.SemesterId;

            // 1. Fees for this program and semester: tuition 3000 and a lab fee of 500.
            var structure = await Client.PostAsync("/api/v1/fee-structures", new { academicProgramId = setup.ProgramId, academicSemesterId = semester, name = "Journey fees" }, admin);
            Assert.Equal(201, structure.Code);
            var structureId = structure.GuidOf("id");

            foreach (var amount in new[] { 3000m, 500m })
            {
                var definition = await Client.PostAsync("/api/v1/fee-definitions", new { universityId = Guid.NewGuid(), code = $"J{Short(10)}", name = "Fee", description = "x", defaultAmount = amount }, admin);
                Assert.Equal(201, definition.Code);
                Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/items", new { feeDefinitionId = definition.GuidOf("id"), amount = (decimal?)null }, admin)).Code);
            }

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/activate", null, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = semester, academicProgramId = setup.ProgramId, dueDate = Today.AddDays(30) }, admin)).Code);

            var invoice = await Client.PostAsync("/api/v1/invoices", new { studentId = student.Id, academicSemesterId = semester }, admin);
            Assert.Equal(201, invoice.Code);
            var invoiceId = invoice.GuidOf("id");

            // 2. The student sees what they owe, and is not blocked yet.
            var finance = await Client.GetAsync("/api/v1/me/finance", token);
            Assert.Equal(200, finance.Code);
            Assert.Equal(3500m, finance.Prop("balance").GetProperty("outstanding").GetDecimal());
            Assert.False(finance.Prop("blocksRegistration").GetBoolean());

            // 3. A hold on registration: the student cannot register, and sees why.
            var hold = await Client.PostAsync("/api/v1/financial-holds", new { studentId = student.Id, areas = new[] { "Registration" }, note = "Fees not settled" }, admin);
            Assert.Equal(201, hold.Code);

            async Task<ApiResponse> SelfRegisterAsync()
                => await Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = semester, courseSectionId = setup.Courses[0].SectionId }, token, TestApi.NewIdempotencyKey());

            var blocked = await SelfRegisterAsync();
            Assert.Equal(422, blocked.Code);
            Assert.Equal("FINANCIAL_HOLD", blocked.ErrorCode);

            var blockedFinance = await Client.GetAsync("/api/v1/me/finance", token);
            Assert.Equal(1, blockedFinance.Prop("activeHolds").GetInt32());
            Assert.True(blockedFinance.Prop("blocksRegistration").GetBoolean());

            // 4. A student cannot pay their own invoice through the staff endpoint.
            var selfPay = await Client.PostAsync($"/api/v1/invoices/{invoiceId}/payments", new { amount = 3500m, method = "Cash" }, token, Key());
            Assert.Equal(403, selfPay.Code);

            // 5. The cashier takes the full amount. The invoice is paid, but a manual hold stays until someone releases it.
            var payment = await Client.PostAsync($"/api/v1/invoices/{invoiceId}/payments", new { amount = 3500m, method = "Cash" }, admin, Key());
            Assert.Equal(201, payment.Code);
            var paymentId = payment.GuidOf("id");

            var paid = await Client.GetAsync($"/api/v1/me/invoices/{invoiceId}", token);
            Assert.Equal(200, paid.Code);
            Assert.Equal("Paid", paid.Str("status"));
            Assert.Equal(0m, (await Client.GetAsync("/api/v1/me/finance", token)).Prop("balance").GetProperty("outstanding").GetDecimal());

            Assert.Equal("FINANCIAL_HOLD", (await SelfRegisterAsync()).ErrorCode);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/financial-holds/{hold.GuidOf("id")}/release", new { note = "Paid in full" }, admin)).Code);

            // 6. Now the student registers by themselves, and a replay of the same request does not take a second seat.
            var key = TestApi.NewIdempotencyKey();
            var body = new { studentId = student.Id, academicSemesterId = semester, courseSectionId = setup.Courses[0].SectionId };

            var registered = await Client.PostAsync("/api/v1/registrations", body, token, key);
            Assert.Equal(201, registered.Code);

            var replay = await Client.PostAsync("/api/v1/registrations", body, token, key);
            Assert.Equal(200, replay.Code);
            Assert.Equal(registered.Prop("registration").GetProperty("id").GetGuid(), replay.Prop("registration").GetProperty("id").GetGuid());

            // 7. The student drops the course and the finance officer refunds the lab fee; a second person approves.
            var registrationId = registered.Prop("registration").GetProperty("id").GetGuid();
            var enrollmentId = registered.Prop("enrollment").GetProperty("id").GetGuid();

            var dropped = await Client.PostAsync($"/api/v1/registrations/{registrationId}/drop", new { enrollmentIds = new[] { enrollmentId } }, token);
            Assert.Equal(200, dropped.Code);

            var officerUser = await _api.CreateUserAsync("journeyofficer", role: RoleNames.FinanceOfficer, scopeType: ScopeType.Global);
            var officer = await _api.LoginTokenAsync(officerUser.UserName, officerUser.Password);

            var refund = await Client.PostAsync("/api/v1/refunds", new { paymentId, amount = 500m, reason = "Dropped the course" }, officer);
            Assert.Equal(201, refund.Code);
            var refundId = refund.GuidOf("id");

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/refunds/{refundId}/approve", null, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/refunds/{refundId}/process", null, admin)).Code);

            // 8. The student sees the result of everything on their own account.
            var after = await Client.GetAsync($"/api/v1/me/invoices/{invoiceId}", token);
            Assert.Equal(200, after.Code);
            Assert.Equal("PartiallyPaid", after.Str("status"));
            Assert.Equal(3000m, after.Prop("paidAmount").GetDecimal());
            Assert.Equal(500m, after.Prop("balance").GetDecimal());

            Assert.Equal(200, (await Client.GetAsync("/api/v1/me/payments", token)).Code);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Journey 3: two departments. Someone with a department scope sees their own department and nothing of the other.
        // ------------------------------------------------------------------------------------------------------------
        [Fact]
        public async Task Journey_a_department_scope_reaches_across_modules_but_stops_at_the_department()
        {
            var admin = await _api.AdminTokenAsync();
            var mine = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var theirs = await GradingScenario.BuildAsync(_api, admin, students: 1);

            Assert.NotEqual(mine.Setup.DepartmentId, theirs.Setup.DepartmentId);

            var dean = await RoleUsers.TokenAsync(
                _api,
                "Dean",
                [Permissions.Students.Read, Permissions.Grades.Read, Permissions.Transcripts.Read, Permissions.Attendance.Read, Permissions.Assessments.Read, Permissions.Registration.Read],
                ScopeType.Department,
                mine.Setup.DepartmentId);

            var myStudent = mine.Students[0].Id;
            var theirStudent = theirs.Students[0].Id;

            // Students.
            Assert.Equal(200, (await Client.GetAsync($"/api/v1/students/{myStudent}", dean)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/students/{theirStudent}", dean)).Code);

            // Grades of a student, and the grade sheet of a section.
            Assert.Equal(200, (await Client.GetAsync($"/api/v1/grades/students/{myStudent}", dean)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/grades/students/{theirStudent}", dean)).Code);

            Assert.Equal(200, (await mine.SheetAsync(_api, dean)).Code);
            Assert.Equal(403, (await theirs.SheetAsync(_api, dean)).Code);

            // Attendance report of a section, and of a student in a section.
            Assert.Equal(200, (await Client.GetAsync($"/api/v1/attendance/reports/sections/{mine.SectionId}", dean)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/attendance/reports/sections/{theirs.SectionId}", dean)).Code);
            Assert.Equal(200, (await Client.GetAsync($"/api/v1/attendance/reports/students/{myStudent}/sections/{mine.SectionId}", dean)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/attendance/reports/students/{theirStudent}/sections/{theirs.SectionId}", dean)).Code);

            // Transcript: the other department is refused before anything is calculated.
            var own = await Client.GetAsync($"/api/v1/transcripts/students/{myStudent}/history", dean);
            Assert.NotEqual(403, own.Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/transcripts/students/{theirStudent}/history", dean)).Code);

            // Read permissions are not write permissions, even inside the department.
            Assert.Equal(403, (await mine.StepAsync(_api, dean, "submit")).Code);

            var session = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = mine.SectionId, sessionDate = Today.AddDays(-2), startTime = "09:00:00" }, dean);
            Assert.Equal(403, session.Code);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Journey 4: an instructor assigned to one section works in it across modules and nowhere else.
        // ------------------------------------------------------------------------------------------------------------
        [Fact]
        public async Task Journey_an_instructor_teaches_their_own_section_and_is_refused_the_next_one()
        {
            var admin = await _api.AdminTokenAsync();
            var own = await GradingScenario.BuildAsync(_api, admin, students: 2);
            var other = await GradingScenario.BuildAsync(_api, admin, students: 1);

            var teacher = await RoleUsers.TokenAsync(
                _api,
                "Lecturer",
                [Permissions.Attendance.Read, Permissions.Attendance.Record, Permissions.Assessments.Read, Permissions.Assessments.Manage, Permissions.Grades.Read, Permissions.Grades.Enter],
                ScopeType.Section,
                own.SectionId);

            // Attendance: opens and marks in their own section, closes it, and reads the result.
            var session = await OpenAttendanceAsync(teacher, own.SectionId, daysAgo: 30);

            var marked = await Client.PostAsync($"/api/v1/attendance/sessions/{session}/marks", new { marks = new[] { new { studentId = own.Students[0].Id, status = "Present" }, new { studentId = own.Students[1].Id, status = "Absent" } } }, teacher);
            Assert.Equal(200, marked.Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{session}/close", null, teacher)).Code);

            // The section report is the one an instructor reads (the per-student report is checked against the student's
            // department, which a section scope does not cover).
            var report = await Client.GetAsync($"/api/v1/attendance/reports/sections/{own.SectionId}", teacher);
            Assert.Equal(200, report.Code);
            Assert.Equal(0m, report.Body.EnumerateArray().Single(s => s.GetProperty("studentId").GetGuid() == own.Students[1].Id).GetProperty("percentage").GetDecimal());
            Assert.Equal(100m, report.Body.EnumerateArray().Single(s => s.GetProperty("studentId").GetGuid() == own.Students[0].Id).GetProperty("percentage").GetDecimal());

            // The same instructor cannot open a session, or mark one, in the neighbouring section.
            Assert.Equal(403, (await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = other.SectionId, sessionDate = Today.AddDays(-30), startTime = "09:00:00" }, teacher)).Code);

            var foreignSession = await OpenAttendanceAsync(admin, other.SectionId, daysAgo: 29);
            var foreignMark = await Client.PostAsync($"/api/v1/attendance/sessions/{foreignSession}/marks", new { marks = new[] { new { studentId = other.Students[0].Id, status = "Present" } } }, teacher);
            Assert.Equal(403, foreignMark.Code);

            Assert.Equal(403, (await Client.GetAsync($"/api/v1/attendance/reports/sections/{other.SectionId}", teacher)).Code);

            // Grades: enters in their own section only.
            Assert.Equal(200, (await own.EnterAsync(_api, teacher, own.Students[0].Id)).Code);
            Assert.Equal(403, (await other.EnterAsync(_api, teacher, other.Students[0].Id)).Code);

            // Assessments: the scheme of their own section is theirs to manage (the state rule answers, not the scope),
            // the scheme of the neighbouring section is not.
            var ownScheme = await Client.PostAsync($"/api/v1/assessments/{own.AssessmentId}/components", new { name = "Extra", type = "Quiz", weight = 1m, maxScore = 10m, dueDate = Today.AddDays(-80) }, teacher);
            Assert.NotEqual(403, ownScheme.Code);

            var foreignScheme = await Client.PostAsync($"/api/v1/assessments/{other.AssessmentId}/components", new { name = "Extra", type = "Quiz", weight = 1m, maxScore = 10m, dueDate = Today.AddDays(-80) }, teacher);
            Assert.Equal(403, foreignScheme.Code);

            // Teaching is not administering: no exam sessions, no review of grades.
            var examSession = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = own.Setup.Calendar.SemesterId, name = $"Not mine {Short(8)}", examType = "Final", startDate = Today.AddDays(-20), endDate = Today.AddDays(-10) }, teacher);
            Assert.Equal(403, examSession.Code);

            Assert.Equal(403, (await own.StepAsync(_api, teacher, "review")).Code);
        }
    }
}
