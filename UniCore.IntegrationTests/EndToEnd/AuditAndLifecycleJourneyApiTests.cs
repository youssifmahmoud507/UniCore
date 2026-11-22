using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Journeys that check what the system REMEMBERS (the audit trail, with the right actor) and how it behaves when the
    // student's own status changes in the middle of a semester.
    [Collection(ApiCollection.Name)]
    public sealed class AuditAndLifecycleJourneyApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        private async Task<Guid> UserIdAsync(string token)
        {
            var me = await Client.GetAsync("/api/v1/me", token);
            Assert.Equal(200, me.Code);

            return me.GuidOf("userId");
        }

        // How many audit rows exist for this action on this entity, optionally by one actor.
        private async Task<int> AuditCountAsync(string admin, string action, Guid entityId, Guid? actor = null)
        {
            var url = $"/api/v1/audit-logs?action={action}&entityId={entityId}" + (actor is null ? string.Empty : $"&actorUserId={actor}");
            var audit = await Client.GetAsync(url, admin);
            Assert.Equal(200, audit.Code);

            return audit.Prop("totalCount").GetInt32();
        }

        [Fact]
        public async Task Journey_every_step_of_a_students_semester_leaves_an_audit_row_with_the_right_actor()
        {
            var admin = await _api.AdminTokenAsync();
            var adminId = await UserIdAsync(admin);
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1, createAccounts: true);
            var student = scenario.Students[0];
            var studentToken = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            var studentId = await UserIdAsync(studentToken);

            // 1. The student drops the registration the registrar made, and registers again by themselves.
            var mine = await Client.GetAsync($"/api/v1/registrations/me?semesterId={scenario.Setup.Calendar.SemesterId}", studentToken);
            Assert.Equal(200, mine.Code);
            var registrationId = mine.GuidOf("id");
            var enrollmentId = mine.Prop("enrollments").EnumerateArray().First().GetProperty("id").GetGuid();

            var dropped = await Client.PostAsync($"/api/v1/registrations/{registrationId}/drop", new { enrollmentIds = new[] { enrollmentId } }, studentToken);
            Assert.Equal(200, dropped.Code);
            Assert.Equal(1, await AuditCountAsync(admin, "ENROLLMENT_DROPPED", registrationId, studentId));

            var again = await Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = scenario.Setup.Calendar.SemesterId, courseSectionId = scenario.SectionId }, studentToken, TestApi.NewIdempotencyKey());
            Assert.True(again.Code is 200 or 201);

            // The registrar's own registration is recorded under the admin, the student's under the student.
            Assert.True(await AuditCountAsync(admin, "ENROLLMENT_REGISTERED", registrationId, adminId) >= 1);

            // 2. The grading chain: each step is recorded against the section, by the person who did it.
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);
            var reviewerId = await UserIdAsync(reviewer);
            var approverId = await UserIdAsync(approver);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            Assert.Equal(1, await AuditCountAsync(admin, "GRADES_SUBMITTED", scenario.SectionId, adminId));
            Assert.Equal(1, await AuditCountAsync(admin, "GRADES_REVIEWED", scenario.SectionId, reviewerId));
            Assert.Equal(1, await AuditCountAsync(admin, "GRADES_APPROVED", scenario.SectionId, approverId));
            Assert.Equal(1, await AuditCountAsync(admin, "GRADES_PUBLISHED", scenario.SectionId, adminId));

            // Nobody else is credited with the review or the approval.
            Assert.Equal(0, await AuditCountAsync(admin, "GRADES_REVIEWED", scenario.SectionId, approverId));
            Assert.Equal(0, await AuditCountAsync(admin, "GRADES_APPROVED", scenario.SectionId, reviewerId));

            // 3. The official transcript is recorded against its snapshot.
            var issued = await Client.PostAsync($"/api/v1/transcripts/students/{student.Id}/snapshots", null, admin);
            Assert.Equal(200, issued.Code);
            Assert.Equal(1, await AuditCountAsync(admin, "TRANSCRIPT_ISSUED", issued.GuidOf("snapshotId"), adminId));

            // 4. The audit trail is not open to a student.
            var denied = await Client.GetAsync($"/api/v1/audit-logs?entityId={scenario.SectionId}", studentToken);
            Assert.Equal(403, denied.Code);
        }

        [Fact]
        public async Task Journey_a_student_on_leave_cannot_register_or_drop_and_can_again_after_returning()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            var semester = setup.Calendar.SemesterId;

            Task<ApiResponse> RegisterAsync(int course)
                => Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = semester, courseSectionId = setup.Courses[course].SectionId }, token, TestApi.NewIdempotencyKey());

            var first = await RegisterAsync(0);
            Assert.Equal(201, first.Code);
            var registrationId = first.Prop("registration").GetProperty("id").GetGuid();
            var enrollmentId = first.Prop("enrollment").GetProperty("id").GetGuid();

            // The student goes on leave in the middle of the semester.
            var status = $"/api/v1/students/{student.Id}/status";
            var onLeave = await Client.PostAsync(status, new { newStatus = "OnLeave", effectiveDate = Today, reason = "medical leave" }, admin);
            Assert.Equal(200, onLeave.Code);

            var blockedRegister = await RegisterAsync(1);
            Assert.Equal(422, blockedRegister.Code);
            Assert.Equal("STUDENT_INACTIVE", blockedRegister.ErrorCode);

            var blockedDrop = await Client.PostAsync($"/api/v1/registrations/{registrationId}/drop", new { enrollmentIds = new[] { enrollmentId } }, token);
            Assert.Equal(422, blockedDrop.Code);
            Assert.Equal("STUDENT_INACTIVE", blockedDrop.ErrorCode);

            // The seat they already hold is untouched by the failed attempts.
            var mine = await Client.GetAsync($"/api/v1/registrations/me?semesterId={semester}", token);
            Assert.Equal(200, mine.Code);
            Assert.Equal(1, mine.Prop("enrollments").GetArrayLength());

            // Back from leave: both actions work again.
            var back = await Client.PostAsync(status, new { newStatus = "Active", effectiveDate = Today }, admin);
            Assert.Equal(200, back.Code);

            Assert.Equal(201, (await RegisterAsync(1)).Code);

            var dropped = await Client.PostAsync($"/api/v1/registrations/{registrationId}/drop", new { enrollmentIds = new[] { enrollmentId } }, token);
            Assert.Equal(200, dropped.Code);
        }

        [Fact]
        public async Task Journey_a_graduated_student_keeps_their_transcript_but_can_no_longer_register()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1, createAccounts: true);
            var student = scenario.Students[0];
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            var graduated = await Client.PostAsync($"/api/v1/students/{student.Id}/status", new { newStatus = "Graduated", effectiveDate = Today }, admin);
            Assert.Equal(200, graduated.Code);

            // The academic record outlives the status.
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            var history = await Client.GetAsync($"/api/v1/transcripts/students/{student.Id}/history", token);
            Assert.Equal(200, history.Code);
            Assert.Equal(1, history.Prop("semesters").GetArrayLength());

            var issued = await Client.PostAsync($"/api/v1/transcripts/students/{student.Id}/snapshots", null, admin);
            Assert.Equal(200, issued.Code);

            // A graduate cannot take a new seat.
            var other = await AcademicScenario.BuildAsync(_api, admin);
            var attempt = await Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = other.Calendar.SemesterId, courseSectionId = other.Courses[0].SectionId }, admin, TestApi.NewIdempotencyKey());
            Assert.Equal(422, attempt.Code);
            Assert.Equal("STUDENT_INACTIVE", attempt.ErrorCode);
        }
    }
}
