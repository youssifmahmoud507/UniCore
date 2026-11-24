using Microsoft.Extensions.DependencyInjection;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // A student asks for a withdrawal or a leave of absence, the workflow approves, and the request is really carried out:
    // the enrollment is withdrawn, the student goes on leave. The workflow event reaches the handler through the outbox.
    [Collection(ApiCollection.Name)]
    public sealed class StudentRequestsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        private OutboxProcessor Processor => fixture.Services.GetRequiredService<OutboxProcessor>();

        private async Task<string> TokenAsync(string prefix, string role, ScopeType scopeType, Guid? scopeId)
        {
            var user = await _api.CreateUserAsync(prefix, role, scopeType, scopeId);

            return await _api.LoginTokenAsync(user.UserName, user.Password);
        }

        private async Task<string> StatusEventuallyAsync(string path, string token, string expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            var status = string.Empty;

            while (DateTime.UtcNow < deadline)
            {
                await Processor.ProcessBatchAsync(200);
                var read = await Client.GetAsync(path, token);
                Assert.Equal(200, read.Code);
                status = read.Str("status");

                if (status == expected)
                {
                    break;
                }

                await Task.Delay(300);
            }

            return status;
        }

        private Task<ApiResponse> Approve(string token, Guid instance) => Client.PostAsync($"/api/v1/workflows/{instance}/approve", new { comment = "ok" }, token);

        [Fact]
        public async Task Journey_a_withdrawal_request_is_approved_by_three_people_and_the_course_is_withdrawn()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var other = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            var otherToken = await _api.LoginTokenAsync(other.Number, TestApi.DefaultPassword);
            var semester = setup.Calendar.SemesterId;

            Task<ApiResponse> RegisterAsync(int course)
                => Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = semester, courseSectionId = setup.Courses[course].SectionId }, admin, TestApi.NewIdempotencyKey());

            var first = await RegisterAsync(0);
            Assert.Equal(201, first.Code);
            Assert.Equal(201, (await RegisterAsync(1)).Code);
            var registrationId = first.Prop("registration").GetProperty("id").GetGuid();
            var enrollmentId = first.Prop("enrollment").GetProperty("id").GetGuid();

            var body = new { registrationId, enrollmentId, reason = "I cannot follow this course" };

            // While add/drop is open a student drops; a withdrawal request is for after it ended.
            var early = await Client.PostAsync("/api/v1/withdrawal-requests", body, token);
            Assert.Equal(422, early.Code);
            Assert.Equal("WITHDRAWAL_DEADLINE_PASSED", early.ErrorCode);

            Guid requestId = Guid.Empty;
            Guid instanceId = Guid.Empty;

            await AcademicScenario.WithWindowsAsync(_api, admin, AcademicScenario.Windows(addDropEnd: Today.AddDays(-1)), async () =>
            {
                // Someone else's registration is not theirs to withdraw from.
                var foreign = await Client.PostAsync("/api/v1/withdrawal-requests", body, otherToken);
                Assert.Equal(403, foreign.Code);

                var submitted = await Client.PostAsync("/api/v1/withdrawal-requests", body, token);
                Assert.Equal(201, submitted.Code);
                Assert.Equal("Pending", submitted.Str("status"));
                requestId = submitted.GuidOf("id");
                instanceId = submitted.GuidOf("workflowInstanceId");

                // One open request per enrollment.
                var duplicate = await Client.PostAsync("/api/v1/withdrawal-requests", body, token);
                Assert.Equal(409, duplicate.Code);
                Assert.Equal("WITHDRAWAL_REQUEST_ALREADY_PENDING", duplicate.ErrorCode);
            });

            // Nothing happens until the approvals are done.
            var mine = await Client.GetAsync($"/api/v1/registrations/me?semesterId={semester}", token);
            Assert.Equal(2, mine.Prop("enrollments").EnumerateArray().Count(e => e.GetProperty("status").GetString() == "Registered"));

            var head = await TokenAsync("wdh", RoleNames.DepartmentHead, ScopeType.Department, setup.DepartmentId);
            var dean = await TokenAsync("wdd", RoleNames.Dean, ScopeType.Global, null);
            var registrar = await TokenAsync("wdr", RoleNames.Registrar, ScopeType.Global, null);

            Assert.Equal(200, (await Approve(head, instanceId)).Code);
            Assert.Equal(200, (await Approve(dean, instanceId)).Code);
            Assert.Equal(200, (await Approve(registrar, instanceId)).Code);

            Assert.Equal("Applied", await StatusEventuallyAsync($"/api/v1/withdrawal-requests/{requestId}", token, "Applied"));

            // That course is withdrawn, the other one is untouched.
            mine = await Client.GetAsync($"/api/v1/registrations/me?semesterId={semester}", token);
            var enrollments = mine.Prop("enrollments").EnumerateArray().ToList();
            Assert.Equal("Withdrawn", enrollments.Single(e => e.GetProperty("id").GetGuid() == enrollmentId).GetProperty("status").GetString());
            Assert.Single(enrollments, e => e.GetProperty("status").GetString() == "Registered");

            var audit = await Client.GetAsync($"/api/v1/audit-logs?action=WITHDRAWAL_APPLIED&entityId={requestId}", admin);
            Assert.Equal(200, audit.Code);
            Assert.Equal(1, audit.Prop("totalCount").GetInt32());

            // The student sees the request in their own list; another student does not.
            Assert.Contains(requestId, (await Client.GetAsync("/api/v1/withdrawal-requests/mine", token)).Body.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()));
            Assert.Equal(404, (await Client.GetAsync($"/api/v1/withdrawal-requests/{requestId}", otherToken)).Code);
        }

        [Fact]
        public async Task Journey_a_leave_request_can_be_rejected_and_then_approved_and_puts_the_student_on_leave()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            var semester = setup.Calendar.SemesterId;

            var head = await TokenAsync("lvh", RoleNames.DepartmentHead, ScopeType.Department, setup.DepartmentId);
            var dean = await TokenAsync("lvd", RoleNames.Dean, ScopeType.Global, null);

            object Body(string reason) => new { studentId = student.Id, startDate = Today, expectedReturnDate = Today.AddDays(120), reason };

            // The return date must come after the start, and the leave cannot start in the past.
            Assert.Equal(400, (await Client.PostAsync("/api/v1/leave-requests", new { studentId = student.Id, startDate = Today, expectedReturnDate = Today, reason = "x" }, token)).Code);
            Assert.Equal(400, (await Client.PostAsync("/api/v1/leave-requests", new { studentId = student.Id, startDate = Today.AddDays(-3), expectedReturnDate = Today.AddDays(30), reason = "x" }, token)).Code);

            // 1. Rejected by the department head: nothing changes and the student can ask again.
            var first = await Client.PostAsync("/api/v1/leave-requests", Body("Medical leave"), token);
            Assert.Equal(201, first.Code);
            var firstId = first.GuidOf("id");

            var again = await Client.PostAsync("/api/v1/leave-requests", Body("Medical leave"), token);
            Assert.Equal(409, again.Code);
            Assert.Equal("LEAVE_REQUEST_ALREADY_PENDING", again.ErrorCode);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/workflows/{first.GuidOf("workflowInstanceId")}/reject", new { comment = "not now" }, head)).Code);
            Assert.Equal("Rejected", await StatusEventuallyAsync($"/api/v1/leave-requests/{firstId}", token, "Rejected"));

            Assert.Equal(201, (await Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = semester, courseSectionId = setup.Courses[0].SectionId }, token, TestApi.NewIdempotencyKey())).Code);

            // 2. The second request is approved by the head and the dean.
            var second = await Client.PostAsync("/api/v1/leave-requests", Body("Medical leave, with a certificate"), token);
            Assert.Equal(201, second.Code);
            var secondId = second.GuidOf("id");
            var instance = second.GuidOf("workflowInstanceId");

            Assert.Equal(200, (await Approve(head, instance)).Code);
            Assert.Equal(200, (await Approve(dean, instance)).Code);

            Assert.Equal("Applied", await StatusEventuallyAsync($"/api/v1/leave-requests/{secondId}", token, "Applied"));

            // The student is on leave: a new registration is refused.
            var blocked = await Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = semester, courseSectionId = setup.Courses[1].SectionId }, admin, TestApi.NewIdempotencyKey());
            Assert.Equal(422, blocked.Code);
            Assert.Equal("STUDENT_INACTIVE", blocked.ErrorCode);

            // The status change is in the audit trail under the person who approved last.
            var audit = await Client.GetAsync($"/api/v1/audit-logs?action=LEAVE_APPLIED&entityId={secondId}", admin);
            Assert.Equal(200, audit.Code);
            Assert.Equal(1, audit.Prop("totalCount").GetInt32());
        }
    }
}
