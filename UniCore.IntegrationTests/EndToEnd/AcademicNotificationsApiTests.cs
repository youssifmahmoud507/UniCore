using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // The students hear about what the academic modules do to them. The events reach the handlers through the outbox, so each check
    // processes what is due and looks again (the API's own background job may process the same messages).
    [Collection(ApiCollection.Name)]
    public sealed class AcademicNotificationsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;
        private readonly ApiFixture _fixture = fixture;

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        private OutboxProcessor Processor => fixture.Services.GetRequiredService<OutboxProcessor>();

        private async Task<List<JsonElement>> MineAsync(string token)
        {
            var response = await Client.GetAsync("/api/v1/me/notifications?pageSize=100", token);
            Assert.Equal(200, response.Code);

            return [.. response.Prop("items").EnumerateArray()];
        }

        private async Task<bool> EventuallyAsync(Func<Task<bool>> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);

            while (DateTime.UtcNow < deadline)
            {
                await Processor.ProcessBatchAsync(200);

                if (await condition())
                {
                    return true;
                }

                await Task.Delay(300);
            }

            return await condition();
        }

        private static IEnumerable<JsonElement> OfType(IEnumerable<JsonElement> items, string type) => items.Where(n => n.GetProperty("type").GetString() == type);

        // Waits for one notification of the type and returns its content.
        private async Task<string> ContentOfAsync(string token, string type, Func<JsonElement, bool>? where = null)
        {
            Assert.True(await EventuallyAsync(async () => OfType(await MineAsync(token), type).Any(n => where is null || where(n))), $"No {type} notification arrived.");

            return OfType(await MineAsync(token), type).First(n => where is null || where(n)).GetProperty("content").GetString()!;
        }

        private async Task<bool> HasAsync(string token, string type) => OfType(await MineAsync(token), type).Any();

        [Fact]
        public async Task Journey_a_student_is_told_at_every_step_of_the_semester()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2, publishAssessment: false, createAccounts: true);
            var present = scenario.Students[0];
            var absent = scenario.Students[1];
            var presentToken = await _api.LoginTokenAsync(present.Number, TestApi.DefaultPassword);
            var absentToken = await _api.LoginTokenAsync(absent.Number, TestApi.DefaultPassword);

            // 1. The registrar registered them: both are told.
            Assert.Contains("registered in", await ContentOfAsync(presentToken, "ENROLLMENT_CONFIRMED"));
            Assert.Contains("registered in", await ContentOfAsync(absentToken, "ENROLLMENT_CONFIRMED"));

            // 2. The grading scheme is published: the whole roster hears.
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/assessments/{scenario.AssessmentId}/publish", null, admin)).Code);
            Assert.Contains("2 components", await ContentOfAsync(presentToken, "ASSESSMENT_PUBLISHED"));
            await ContentOfAsync(absentToken, "ASSESSMENT_PUBLISHED");

            // 3. Attendance: only the student who was absent gets a warning (it cannot be switched off).
            var course = scenario.Setup.Courses[0].CourseId;
            Assert.Equal(201, (await Client.PostAsync("/api/v1/attendance/policies", new { courseId = course, minimumPercentage = 80m, warningPercentage = 90m, lateThresholdMinutes = 10, isExamBlocking = true }, admin)).Code);

            var session = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = scenario.SectionId, sessionDate = Today.AddDays(-30), startTime = "09:00:00" }, admin);
            Assert.Equal(201, session.Code);
            var sessionId = session.GuidOf("id");
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{sessionId}/marks", new { marks = new[] { new { studentId = present.Id, status = "Present" }, new { studentId = absent.Id, status = "Absent" } } }, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{sessionId}/close", null, admin)).Code);

            var warning = await ContentOfAsync(absentToken, "ATTENDANCE_WARNING");
            Assert.Contains("exam", warning);
            Assert.False(await HasAsync(presentToken, "ATTENDANCE_WARNING"));

            // 4. An exam is published and then cancelled: both students are told, with the date.
            var examSession = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = scenario.Setup.Calendar.SemesterId, name = $"Notify finals {Guid.NewGuid():N}"[..28], examType = "Final", startDate = Today.AddDays(-20), endDate = Today.AddDays(-10) }, admin);
            Assert.Equal(201, examSession.Code);
            var exam = await Client.PostAsync("/api/v1/exams", new { courseSectionId = scenario.SectionId, examSessionId = examSession.GuidOf("id"), examType = "Final", maxScore = 100m, durationMinutes = 120 }, admin);
            Assert.Equal(201, exam.Code);
            var examId = exam.GuidOf("id");

            var examDate = Today.AddDays(-15);
            Assert.Equal(200, (await Client.PutAsync($"/api/v1/exams/{examId}/schedule", new { examDate, startTime = "09:00", endTime = "12:00" }, admin)).Code);
            var room = await RoomSeed.RoomAsync(_fixture, _api, admin, capacity: 40);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/exams/{examId}/rooms", new { roomId = room }, admin)).Code);
            var invigilator = await AcademicScenario.CreateInstructorAsync(_api, admin, scenario.Setup.DepartmentId);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/exams/{examId}/invigilators", new { roomId = room, instructorId = invigilator, role = "Chief" }, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/exams/{examId}/publish", null, admin)).Code);

            var scheduled = await ContentOfAsync(presentToken, "EXAM_PUBLISHED");
            Assert.Contains(examDate.ToString("yyyy-MM-dd"), scheduled);
            Assert.Contains("09:00", scheduled);
            await ContentOfAsync(absentToken, "EXAM_PUBLISHED");

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/exams/{examId}/cancel", null, admin)).Code);
            await ContentOfAsync(presentToken, "EXAM_CANCELLED");
            await ContentOfAsync(absentToken, "EXAM_CANCELLED");

            // 5. The grades are published: each student is told their own result (Quiz 20/20 and Final 50/100 give 70 percent).
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);
            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            Assert.Contains("70 percent", await ContentOfAsync(presentToken, "GRADE_PUBLISHED"));
            await ContentOfAsync(absentToken, "GRADE_PUBLISHED");

            // 6. A correction approved by the head and the dean reaches the student with the old and the new result.
            var sheet = await scenario.SheetAsync(_api, admin);
            var gradeId = sheet.Prop("items").EnumerateArray().Single(g => g.GetProperty("studentId").GetGuid() == present.Id).GetProperty("id").GetGuid();

            var requester = await RoleUsers.TokenAsync(_api, "NotifyChanger", [Permissions.Grades.Read, Permissions.GradeChanges.Request], ScopeType.Department, scenario.Setup.DepartmentId);
            var head = await TokenAsync("nfh", RoleNames.DepartmentHead, ScopeType.Department, scenario.Setup.DepartmentId);
            var dean = await TokenAsync("nfd", RoleNames.Dean, ScopeType.Global, null);

            var change = await Client.PostAsync("/api/v1/grade-changes", new { gradeId, assessmentComponentId = scenario.FinalId, newScore = 80m, reason = "The final exam was marked wrongly" }, requester);
            Assert.Equal(201, change.Code);
            var instanceId = change.GuidOf("workflowInstanceId");
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/workflows/{instanceId}/approve", new { comment = "ok" }, head)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/workflows/{instanceId}/approve", new { comment = "ok" }, dean)).Code);

            var corrected = await ContentOfAsync(presentToken, "GRADE_CORRECTED");
            Assert.Contains("70 percent", corrected);
            Assert.Contains("88 percent", corrected);
            Assert.False(await HasAsync(absentToken, "GRADE_CORRECTED"));

            // 7. The official transcript: the student is told, and the notification points to the snapshot.
            var issued = await Client.PostAsync($"/api/v1/transcripts/students/{present.Id}/snapshots", null, admin);
            Assert.Equal(200, issued.Code);
            var snapshotId = issued.GuidOf("snapshotId");
            await ContentOfAsync(presentToken, "TRANSCRIPT_ISSUED", n => n.GetProperty("referenceId").ValueKind != JsonValueKind.Null && n.GetProperty("referenceId").GetGuid() == snapshotId);
            Assert.False(await HasAsync(absentToken, "TRANSCRIPT_ISSUED"));
        }

        [Fact]
        public async Task A_dropped_course_is_confirmed_to_that_student_only_and_a_switched_off_type_stays_quiet()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2, createAccounts: true);
            var leaving = scenario.Students[0];
            var staying = scenario.Students[1];
            var leavingToken = await _api.LoginTokenAsync(leaving.Number, TestApi.DefaultPassword);
            var stayingToken = await _api.LoginTokenAsync(staying.Number, TestApi.DefaultPassword);

            // The student who stays does not want to hear about published grades; the type is optional, so they can switch it off.
            Assert.Equal(200, (await Client.PutAsync("/api/v1/me/notifications/preferences/GRADE_PUBLISHED", new { inAppEnabled = false, emailEnabled = false }, stayingToken)).Code);

            // The attendance warning is mandatory and cannot be switched off.
            var mandatory = await Client.PutAsync("/api/v1/me/notifications/preferences/ATTENDANCE_WARNING", new { inAppEnabled = false, emailEnabled = false }, stayingToken);
            Assert.Equal(422, mandatory.Code);
            Assert.Equal("NOTIFICATION_PREFERENCE_MANDATORY", mandatory.ErrorCode);

            // One student drops their own registration.
            var mine = await Client.GetAsync($"/api/v1/registrations/me?semesterId={scenario.Setup.Calendar.SemesterId}", leavingToken);
            Assert.Equal(200, mine.Code);
            var enrollmentId = mine.Prop("enrollments").EnumerateArray().First().GetProperty("id").GetGuid();
            var dropped = await Client.PostAsync($"/api/v1/registrations/{mine.GuidOf("id")}/drop", new { enrollmentIds = new[] { enrollmentId } }, leavingToken);
            Assert.Equal(200, dropped.Code);

            Assert.Contains("no longer registered", await ContentOfAsync(leavingToken, "ENROLLMENT_DROPPED"));
            Assert.False(await HasAsync(stayingToken, "ENROLLMENT_DROPPED"));

            // Grades are published for the student who stayed: nothing arrives, because they switched it off.
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, staying.Id)).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            // A mandatory-free control: the optional registration notification of the same student did arrive earlier, so the
            // outbox has certainly run; the switched-off type never shows up.
            await ContentOfAsync(stayingToken, "ENROLLMENT_CONFIRMED");
            await Processor.ProcessBatchAsync(200);
            Assert.False(await HasAsync(stayingToken, "GRADE_PUBLISHED"));
        }

        [Fact]
        public async Task A_published_timetable_is_told_once_to_the_students_of_the_scheduled_sections_only()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2);
            var scheduled = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var other = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var scheduledToken = await _api.LoginTokenAsync(scheduled.Number, TestApi.DefaultPassword);
            var otherToken = await _api.LoginTokenAsync(other.Number, TestApi.DefaultPassword);
            var semester = setup.Calendar.SemesterId;

            // The first student takes both courses, the second only the one that is not in the timetable.
            Assert.Equal(201, (await Client.PostAsync("/api/v1/registrations", new { studentId = scheduled.Id, academicSemesterId = semester, courseSectionId = setup.Courses[0].SectionId }, admin, TestApi.NewIdempotencyKey())).Code);
            Assert.Equal(201, (await Client.PostAsync("/api/v1/registrations", new { studentId = scheduled.Id, academicSemesterId = semester, courseSectionId = setup.Courses[1].SectionId }, admin, TestApi.NewIdempotencyKey())).Code);
            Assert.Equal(201, (await Client.PostAsync("/api/v1/registrations", new { studentId = other.Id, academicSemesterId = semester, courseSectionId = setup.Courses[1].SectionId }, admin, TestApi.NewIdempotencyKey())).Code);

            var schedule = await Client.PostAsync("/api/v1/schedules", new { academicSemesterId = semester, name = $"Timetable {Guid.NewGuid():N}"[..20] }, admin);
            Assert.Equal(201, schedule.Code);
            var scheduleId = schedule.GuidOf("id");

            var added = await Client.PostAsync($"/api/v1/schedules/{scheduleId}/slots", new { dayOfWeek = "Tuesday", startTime = "09:00", endTime = "11:00" }, admin);
            Assert.Equal(200, added.Code);
            var slotId = added.Prop("slots").EnumerateArray().Single().GetProperty("id").GetGuid();

            var room = await RoomSeed.RoomAsync(_fixture, _api, admin, capacity: 40);
            Assert.Equal(201, (await Client.PostAsync($"/api/v1/schedules/{scheduleId}/meetings", new { courseSectionId = setup.Courses[0].SectionId, scheduleSlotId = slotId, roomId = room }, admin)).Code);

            // Meetings are added to a draft: nobody is told yet.
            await Processor.ProcessBatchAsync(200);
            Assert.False(await HasAsync(scheduledToken, "SCHEDULE_PUBLISHED"));

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/schedules/{scheduleId}/publish", null, admin)).Code);

            await ContentOfAsync(scheduledToken, "SCHEDULE_PUBLISHED", n => n.GetProperty("referenceId").ValueKind != JsonValueKind.Null && n.GetProperty("referenceId").GetGuid() == scheduleId);

            // Once, although the student is in two sections, and not the student whose course is not in the timetable.
            await Processor.ProcessBatchAsync(200);
            Assert.Single(OfType(await MineAsync(scheduledToken), "SCHEDULE_PUBLISHED"));
            Assert.False(await HasAsync(otherToken, "SCHEDULE_PUBLISHED"));
        }

        private async Task<string> TokenAsync(string prefix, string role, ScopeType scopeType, Guid? scopeId)
        {
            var user = await _api.CreateUserAsync(prefix, role, scopeType, scopeId);

            return await _api.LoginTokenAsync(user.UserName, user.Password);
        }
    }
}
