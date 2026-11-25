using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // A Published or Locked grade changes only through a grade change request: GradeChanges.Request starts the GRADE_CHANGE workflow
    // (department head, then dean) and the correction is applied when it completes. The workflow event reaches the handler through
    // the outbox, so the tests wait for the request to leave Pending.
    [Collection(ApiCollection.Name)]
    public sealed class GradeChangesApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        // Quiz 20/20 and Final 50/100 give 70 percent, published.
        private async Task<(GradingScenario Scenario, Guid GradeId)> PublishedAsync(string admin, bool publish = true)
        {
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1, createAccounts: true);
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);

            await scenario.EnterAllAsync(_api, admin);

            if (publish)
            {
                Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
                Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
                Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
                Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);
            }

            var sheet = await scenario.SheetAsync(_api, admin);
            Assert.Equal(200, sheet.Code);

            return (scenario, sheet.Prop("items")[0].GetProperty("id").GetGuid());
        }

        private Task<string> RequesterAsync(Guid departmentId)
            => RoleUsers.TokenAsync(_api, "GradeChanger", [Permissions.Grades.Read, Permissions.GradeChanges.Request], ScopeType.Department, departmentId);

        private async Task<string> RoleTokenAsync(string prefix, string role, ScopeType scopeType, Guid? scopeId)
        {
            var user = await _api.CreateUserAsync(prefix, role, scopeType, scopeId);

            return await _api.LoginTokenAsync(user.UserName, user.Password);
        }

        private Task<ApiResponse> RequestAsync(string token, Guid gradeId, Guid componentId, decimal newScore, string? reason = "The final exam was marked wrongly")
            => Client.PostAsync("/api/v1/grade-changes", new { gradeId, assessmentComponentId = componentId, newScore, reason }, token);

        private Task<ApiResponse> ActAsync(string token, Guid instanceId, string action, string? comment = null)
            => Client.PostAsync($"/api/v1/workflows/{instanceId}/{action}", new { comment }, token);

        // The request leaves Pending when the outbox delivers the workflow event.
        private async Task<ApiResponse> SettledAsync(string token, Guid requestId)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            ApiResponse last;

            do
            {
                last = await Client.GetAsync($"/api/v1/grade-changes/{requestId}", token);
                Assert.Equal(200, last.Code);

                if (last.Str("status") != "Pending")
                {
                    return last;
                }

                await Task.Delay(200);
            }
            while (DateTimeOffset.UtcNow < deadline);

            return last;
        }

        private async Task<decimal> PercentageAsync(GradingScenario scenario, string token)
        {
            var sheet = await scenario.SheetAsync(_api, token);
            Assert.Equal(200, sheet.Code);

            return sheet.Prop("items")[0].GetProperty("percentage").GetDecimal();
        }

        [Fact]
        public async Task Journey_a_published_grade_changes_only_after_the_head_and_the_dean_approve()
        {
            var admin = await _api.AdminTokenAsync();
            var (scenario, gradeId) = await PublishedAsync(admin);
            var departmentId = scenario.Setup.DepartmentId;

            var requester = await RequesterAsync(departmentId);
            var head = await RoleTokenAsync("gch", RoleNames.DepartmentHead, ScopeType.Department, departmentId);
            var dean = await RoleTokenAsync("gcd", RoleNames.Dean, ScopeType.Global, null);

            // The final exam goes from 50 to 80: 40 + 48 = 88 percent.
            var requested = await RequestAsync(requester, gradeId, scenario.FinalId, 80m);
            Assert.Equal(201, requested.Code);
            Assert.Equal("Pending", requested.Str("status"));
            Assert.Equal(50m, requested.Prop("oldScore").GetDecimal());
            Assert.Equal(80m, requested.Prop("newScore").GetDecimal());
            var requestId = requested.GuidOf("id");
            var instanceId = requested.GuidOf("workflowInstanceId");

            // Asking does not change anything, and a second open request for the same grade is refused.
            Assert.Equal(70m, await PercentageAsync(scenario, admin));

            var duplicate = await RequestAsync(requester, gradeId, scenario.FinalId, 90m);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("GRADE_CHANGE_ALREADY_PENDING", duplicate.ErrorCode);

            // The dean cannot skip the head; the requester cannot approve their own request.
            var early = await ActAsync(dean, instanceId, "approve");
            Assert.Equal(403, early.Code);
            Assert.Equal("WORKFLOW_NOT_CURRENT_APPROVER", early.ErrorCode);

            Assert.Equal(200, (await ActAsync(head, instanceId, "approve", "The paper was re-marked")).Code);

            // Half approved is still the old grade.
            Assert.Equal("Pending", (await Client.GetAsync($"/api/v1/grade-changes/{requestId}", requester)).Str("status"));
            Assert.Equal(70m, await PercentageAsync(scenario, admin));

            var completed = await ActAsync(dean, instanceId, "approve", "Approved");
            Assert.Equal(200, completed.Code);
            Assert.Equal("Completed", completed.Str("status"));

            var applied = await SettledAsync(requester, requestId);
            Assert.Equal("Applied", applied.Str("status"));
            Assert.Equal(88m, await PercentageAsync(scenario, admin));

            // The student's record shows the new percentage, and the audit trail names who made it official.
            var student = scenario.Students[0];
            var history = await Client.GetAsync($"/api/v1/transcripts/students/{student.Id}/history", admin);
            Assert.Equal(88m, history.Prop("semesters")[0].GetProperty("courses")[0].GetProperty("percentage").GetDecimal());

            var corrected = await Client.GetAsync($"/api/v1/audit-logs?action=GRADE_CORRECTED&entityId={gradeId}", admin);
            Assert.Equal(200, corrected.Code);
            Assert.Equal(1, corrected.Prop("totalCount").GetInt32());

            var appliedAudit = await Client.GetAsync($"/api/v1/audit-logs?action=GRADE_CHANGE_APPLIED&entityId={requestId}", admin);
            Assert.Equal(1, appliedAudit.Prop("totalCount").GetInt32());

            // With nothing pending any more, the grade can be changed again.
            Assert.Equal(201, (await RequestAsync(requester, gradeId, scenario.FinalId, 85m)).Code);

            var listed = await Client.GetAsync($"/api/v1/grade-changes/grades/{gradeId}", requester);
            Assert.Equal(200, listed.Code);
            Assert.Equal(2, listed.Body.GetArrayLength());
        }

        [Fact]
        public async Task Journey_a_rejected_request_leaves_the_grade_alone_and_frees_it_for_a_new_request()
        {
            var admin = await _api.AdminTokenAsync();
            var (scenario, gradeId) = await PublishedAsync(admin);
            var departmentId = scenario.Setup.DepartmentId;

            var requester = await RequesterAsync(departmentId);
            var head = await RoleTokenAsync("gch", RoleNames.DepartmentHead, ScopeType.Department, departmentId);

            var requested = await RequestAsync(requester, gradeId, scenario.FinalId, 100m);
            Assert.Equal(201, requested.Code);
            var requestId = requested.GuidOf("id");

            var rejected = await ActAsync(head, requested.GuidOf("workflowInstanceId"), "reject", "The paper was marked correctly");
            Assert.Equal(200, rejected.Code);

            var settled = await SettledAsync(requester, requestId);
            Assert.Equal("Rejected", settled.Str("status"));
            Assert.Equal(70m, await PercentageAsync(scenario, admin));

            Assert.Equal(1, (await Client.GetAsync($"/api/v1/audit-logs?action=GRADE_CHANGE_REJECTED&entityId={requestId}", admin)).Prop("totalCount").GetInt32());

            // A rejected request does not block the grade.
            Assert.Equal(201, (await RequestAsync(requester, gradeId, scenario.FinalId, 60m)).Code);
        }

        [Fact]
        public async Task A_request_that_could_never_be_applied_is_refused_before_anyone_has_to_approve_it()
        {
            var admin = await _api.AdminTokenAsync();
            var (scenario, gradeId) = await PublishedAsync(admin);
            var requester = await RequesterAsync(scenario.Setup.DepartmentId);

            // The same score is not a change.
            var same = await RequestAsync(requester, gradeId, scenario.FinalId, 50m);
            Assert.Equal(422, same.Code);
            Assert.Equal("GRADE_CHANGE_NO_CHANGE", same.ErrorCode);

            // More than the maximum of the component (the final is out of 100), or a component the grade does not have.
            var tooHigh = await RequestAsync(requester, gradeId, scenario.FinalId, 150m);
            Assert.Equal(400, tooHigh.Code);
            Assert.Equal("GRADE_SCORE_INVALID", tooHigh.ErrorCode);

            var unknown = await RequestAsync(requester, gradeId, Guid.NewGuid(), 10m);
            Assert.Equal(400, unknown.Code);
            Assert.Equal("GRADE_CORRECTION_INVALID", unknown.ErrorCode);

            // A reason is required.
            var noReason = await RequestAsync(requester, gradeId, scenario.FinalId, 60m, reason: "  ");
            Assert.Equal(400, noReason.Code);
            Assert.Equal("GRADE_CHANGE_DATA_INVALID", noReason.ErrorCode);

            // A grade that is not published yet is changed by entering scores, not by a request.
            var (draftScenario, draftGradeId) = await PublishedAsync(admin, publish: false);
            var draftRequester = await RequesterAsync(draftScenario.Setup.DepartmentId);
            var draft = await RequestAsync(draftRequester, draftGradeId, draftScenario.FinalId, 60m);
            Assert.Equal(422, draft.Code);
            Assert.Equal("GRADE_CHANGE_GRADE_NOT_FINAL", draft.ErrorCode);

            // Nothing above left a pending request behind.
            var listed = await Client.GetAsync($"/api/v1/grade-changes/grades/{gradeId}", requester);
            Assert.Equal(200, listed.Code);
            Assert.Equal(0, listed.Body.GetArrayLength());
        }

        [Fact]
        public async Task Only_someone_with_the_permission_in_the_department_of_the_course_can_ask()
        {
            var admin = await _api.AdminTokenAsync();
            var (scenario, gradeId) = await PublishedAsync(admin);

            var student = scenario.Students[0];
            var studentToken = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            Assert.Equal(403, (await RequestAsync(studentToken, gradeId, scenario.FinalId, 80m)).Code);

            // The right permission in another department is not enough.
            var otherDepartment = await RequesterAsync(Guid.NewGuid());
            Assert.Equal(403, (await RequestAsync(otherDepartment, gradeId, scenario.FinalId, 80m)).Code);

            var unknown = await RequestAsync(admin, Guid.NewGuid(), scenario.FinalId, 80m);
            Assert.Equal(404, unknown.Code);
            Assert.Equal("GRADE_NOT_FOUND", unknown.ErrorCode);

            // Reading a request follows the same rule.
            var requester = await RequesterAsync(scenario.Setup.DepartmentId);
            var requested = await RequestAsync(requester, gradeId, scenario.FinalId, 80m);
            Assert.Equal(201, requested.Code);

            Assert.Equal(200, (await Client.GetAsync($"/api/v1/grade-changes/{requested.GuidOf("id")}", requester)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/grade-changes/{requested.GuidOf("id")}", otherDepartment)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/grade-changes/{requested.GuidOf("id")}", studentToken)).Code);
            Assert.Equal(404, (await Client.GetAsync($"/api/v1/grade-changes/{Guid.NewGuid()}", admin)).Code);
        }
    }
}
