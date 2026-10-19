using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class WorkflowApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private async Task<string> TokenAsync(string prefix, string role, ScopeType? scopeType = null, Guid? scopeId = null)
        {
            var user = await _api.CreateUserAsync(prefix, role, scopeType, scopeId);

            return await _api.LoginTokenAsync(user.UserName, user.Password);
        }

        private Task<ApiResponse> StartAsync(string token, string code, Guid? department = null, Guid? reference = null)
            => Client.PostAsync("/api/v1/workflows", new { definitionCode = code, referenceType = "TestRequest", referenceId = reference ?? Guid.NewGuid(), scopeType = department is null ? null : "Department", scopeId = department }, token);

        private Task<ApiResponse> ActAsync(string token, Guid id, string action, string? comment = null)
            => Client.PostAsync($"/api/v1/workflows/{id}/{action}", new { comment }, token);

        private static string NewCode() => $"T_{Guid.NewGuid():N}"[..14].ToUpperInvariant();

        [Fact]
        public async Task A_withdrawal_goes_through_three_steps_and_completes()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("wfs", RoleNames.Student);
            var head = await TokenAsync("wfh", RoleNames.DepartmentHead, ScopeType.Department, department);
            var dean = await TokenAsync("wfd", RoleNames.Dean, ScopeType.Global);
            var registrar = await TokenAsync("wfr", RoleNames.Registrar, ScopeType.Global);

            var started = await StartAsync(student, "WITHDRAWAL_REQUEST", department);
            Assert.Equal(201, started.Code);
            Assert.Equal("InProgress", started.Str("status"));
            Assert.Equal(RoleNames.DepartmentHead, started.Str("currentRole"));
            var id = started.GuidOf("id");

            var first = await ActAsync(head, id, "approve", "ok");
            Assert.Equal(200, first.Code);
            Assert.Equal(RoleNames.Dean, first.Str("currentRole"));

            Assert.Equal(RoleNames.Registrar, (await ActAsync(dean, id, "approve")).Str("currentRole"));

            var last = await ActAsync(registrar, id, "approve", "done");
            Assert.Equal(200, last.Code);
            Assert.Equal("Completed", last.Str("status"));
            Assert.Equal(3, last.Prop("approvals").GetArrayLength());
            Assert.Equal(4, last.Prop("actions").GetArrayLength());

            var again = await ActAsync(registrar, id, "approve");
            Assert.Equal(422, again.Code);
            Assert.Equal("WORKFLOW_INVALID_STATE", again.ErrorCode);
        }

        [Fact]
        public async Task Only_the_right_role_in_the_right_scope_can_act()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("wfs", RoleNames.Student);
            var registrar = await TokenAsync("wfr", RoleNames.Registrar, ScopeType.Global);
            var otherHead = await TokenAsync("wfo", RoleNames.DepartmentHead, ScopeType.Department, Guid.NewGuid());

            var id = (await StartAsync(student, "WITHDRAWAL_REQUEST", department)).GuidOf("id");

            // The student asked for it; the registrar is not the step's role; the other department's head is out of scope.
            foreach (var token in new[] { student, registrar, otherHead })
            {
                var denied = await ActAsync(token, id, "approve");

                Assert.Equal(403, denied.Code);
                Assert.Equal("WORKFLOW_NOT_CURRENT_APPROVER", denied.ErrorCode);
            }

            Assert.Equal(401, (await Client.PostAsync($"/api/v1/workflows/{id}/approve", new { })).Code);
        }

        [Fact]
        public async Task A_request_without_a_scope_can_be_decided_only_by_a_global_role()
        {
            var student = await TokenAsync("wfs", RoleNames.Student);
            var scopedHead = await TokenAsync("wfh", RoleNames.DepartmentHead, ScopeType.Department, Guid.NewGuid());
            var globalHead = await TokenAsync("wfg", RoleNames.DepartmentHead, ScopeType.Global);

            var id = (await StartAsync(student, "COMPLAINT")).GuidOf("id");

            Assert.Equal(403, (await ActAsync(scopedHead, id, "approve")).Code);
            Assert.Equal(200, (await ActAsync(globalHead, id, "approve")).Code);
        }

        [Fact]
        public async Task Nobody_decides_their_own_request()
        {
            var user = await _api.CreateUserAsync("wfself", RoleNames.Student);
            var admin = await _api.AdminTokenAsync();

            var assigned = await Client.PostAsync($"/api/v1/users/{user.Id}/roles", new { role = RoleNames.DepartmentHead, scopeType = "Global" }, admin);
            Assert.Equal(200, assigned.Code);

            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            var id = (await StartAsync(token, "COMPLAINT")).GuidOf("id");

            var denied = await ActAsync(token, id, "approve");
            Assert.Equal(403, denied.Code);
            Assert.Equal("WORKFLOW_SELF_APPROVAL", denied.ErrorCode);

            var inbox = await Client.GetAsync("/api/v1/workflows/inbox", token);
            Assert.Equal(0, inbox.Prop("items").GetArrayLength());
        }

        [Fact]
        public async Task Reject_and_return_need_a_comment_and_a_returned_request_can_be_resubmitted()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("wfs", RoleNames.Student);
            var head = await TokenAsync("wfh", RoleNames.DepartmentHead, ScopeType.Department, department);

            var rejected = (await StartAsync(student, "COMPLAINT", department)).GuidOf("id");
            var noComment = await ActAsync(head, rejected, "reject", " ");
            Assert.Equal(400, noComment.Code);
            Assert.Equal("WORKFLOW_COMMENT_REQUIRED", noComment.ErrorCode);
            Assert.Equal("Rejected", (await ActAsync(head, rejected, "reject", "not valid")).Str("status"));

            var returned = (await StartAsync(student, "LEAVE_REQUEST", department)).GuidOf("id");
            var sent = await ActAsync(head, returned, "return", "fix the dates");
            Assert.Equal("Returned", sent.Str("status"));

            var notRequester = await ActAsync(head, returned, "resubmit");
            Assert.Equal(403, notRequester.Code);
            Assert.Equal("WORKFLOW_NOT_REQUESTER", notRequester.ErrorCode);

            var again = await ActAsync(student, returned, "resubmit", "fixed");
            Assert.Equal(200, again.Code);
            Assert.Equal("InProgress", again.Str("status"));
            Assert.Equal(1, again.Prop("currentStepOrder").GetInt32());
        }

        [Fact]
        public async Task A_request_has_one_live_workflow_and_a_cancelled_one_can_start_again()
        {
            var student = await TokenAsync("wfs", RoleNames.Student);
            var stranger = await TokenAsync("wfx", RoleNames.Student);
            var reference = Guid.NewGuid();

            var first = await StartAsync(student, "APPEAL", null, reference);
            Assert.Equal(201, first.Code);

            var duplicate = await StartAsync(student, "APPEAL", null, reference);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("WORKFLOW_ALREADY_ACTIVE", duplicate.ErrorCode);

            var id = first.GuidOf("id");
            Assert.Equal(403, (await ActAsync(stranger, id, "cancel")).Code);
            Assert.Equal("Cancelled", (await ActAsync(student, id, "cancel", "changed my mind")).Str("status"));

            Assert.Equal(201, (await StartAsync(student, "APPEAL", null, reference)).Code);
        }

        [Fact]
        public async Task The_inbox_shows_a_request_only_to_approvers_who_cover_it()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("wfs", RoleNames.Student);
            var head = await TokenAsync("wfh", RoleNames.DepartmentHead, ScopeType.Department, department);
            var otherHead = await TokenAsync("wfo", RoleNames.DepartmentHead, ScopeType.Department, Guid.NewGuid());

            var id = (await StartAsync(student, "LEAVE_REQUEST", department)).GuidOf("id");

            var mine = await Client.GetAsync("/api/v1/workflows/inbox", head);
            Assert.Equal(200, mine.Code);
            Assert.Contains(mine.Prop("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);

            var other = await Client.GetAsync("/api/v1/workflows/inbox", otherHead);
            Assert.DoesNotContain(other.Prop("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);

            var requests = await Client.GetAsync("/api/v1/workflows/mine", student);
            Assert.Contains(requests.Prop("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        }

        [Fact]
        public async Task A_request_can_be_read_by_its_requester_and_by_staff_that_cover_it_only()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("wfs", RoleNames.Student);
            var other = await TokenAsync("wfx", RoleNames.Student);
            var head = await TokenAsync("wfh", RoleNames.DepartmentHead, ScopeType.Department, department);
            var otherHead = await TokenAsync("wfo", RoleNames.DepartmentHead, ScopeType.Department, Guid.NewGuid());

            var id = (await StartAsync(student, "LEAVE_REQUEST", department)).GuidOf("id");

            Assert.Equal(200, (await Client.GetAsync($"/api/v1/workflows/{id}", student)).Code);
            Assert.Equal(200, (await Client.GetAsync($"/api/v1/workflows/{id}", head)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/workflows/{id}", other)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/workflows/{id}", otherHead)).Code);
            Assert.Equal(401, (await Client.GetAsync($"/api/v1/workflows/{id}")).Code);
            Assert.Equal(404, (await Client.GetAsync($"/api/v1/workflows/{Guid.NewGuid()}", student)).Code);
        }

        [Fact]
        public async Task The_standard_definitions_are_seeded_and_a_new_version_replaces_the_active_one()
        {
            var admin = await _api.AdminTokenAsync();
            var student = await TokenAsync("wfs", RoleNames.Student);

            var seeded = await Client.GetAsync("/api/v1/workflow-definitions?isActive=true&pageSize=100", student);
            Assert.Equal(200, seeded.Code);
            var codes = seeded.Prop("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()).ToList();
            foreach (var code in new[] { "CERTIFICATE_REQUEST", "TRANSCRIPT_REQUEST", "WITHDRAWAL_REQUEST", "LEAVE_REQUEST", "COMPLAINT", "APPEAL", "GRADE_APPEAL" })
            {
                Assert.Contains(code, codes);
            }

            var denied = await Client.PostAsync("/api/v1/workflow-definitions", new { code = NewCode(), name = "Nope", steps = new[] { new { name = "Dean", requiredRole = RoleNames.Dean } } }, student);
            Assert.Equal(403, denied.Code);

            var flow = NewCode();
            var v1 = await Client.PostAsync("/api/v1/workflow-definitions", new { code = flow, name = "Flow", steps = new[] { new { name = "Dean", requiredRole = RoleNames.Dean } } }, admin);
            Assert.Equal(201, v1.Code);
            Assert.Equal(1, v1.Prop("version").GetInt32());

            var v2 = await Client.PostAsync("/api/v1/workflow-definitions", new { code = flow, name = "Flow", steps = new[] { new { name = "Registrar", requiredRole = RoleNames.Registrar }, new { name = "Dean", requiredRole = RoleNames.Dean } } }, admin);
            Assert.Equal(201, v2.Code);
            Assert.Equal(2, v2.Prop("version").GetInt32());

            var versions = await Client.GetAsync($"/api/v1/workflow-definitions?code={flow}", admin);
            Assert.Equal(2, versions.Prop("items").GetArrayLength());
            Assert.Equal(1, versions.Prop("items").EnumerateArray().Count(i => i.GetProperty("isActive").GetBoolean()));

            var started = await StartAsync(student, flow);
            Assert.Equal(2, started.Prop("definitionVersion").GetInt32());
            Assert.Equal(2, started.Prop("totalSteps").GetInt32());

            var bad = await Client.PostAsync("/api/v1/workflow-definitions", new { code = NewCode(), name = "Bad", steps = new[] { new { name = "Student", requiredRole = RoleNames.Student } } }, admin);
            Assert.Equal(400, bad.Code);
            Assert.Equal("WORKFLOW_DEFINITION_DATA_INVALID", bad.ErrorCode);
        }

        [Fact]
        public async Task An_unknown_or_deactivated_definition_cannot_start()
        {
            var admin = await _api.AdminTokenAsync();
            var student = await TokenAsync("wfs", RoleNames.Student);

            var unknown = await StartAsync(student, NewCode());
            Assert.Equal(404, unknown.Code);
            Assert.Equal("WORKFLOW_DEFINITION_NOT_FOUND", unknown.ErrorCode);

            var code = NewCode();
            var created = await Client.PostAsync("/api/v1/workflow-definitions", new { code, name = "Soon off", steps = new[] { new { name = "Dean", requiredRole = RoleNames.Dean } } }, admin);
            Assert.Equal(201, created.Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/workflow-definitions/{created.GuidOf("id")}/deactivate", null, admin)).Code);

            var inactive = await StartAsync(student, code);
            Assert.Equal(422, inactive.Code);
            Assert.Equal("WORKFLOW_DEFINITION_INACTIVE", inactive.ErrorCode);

            var invalid = await Client.PostAsync("/api/v1/workflows", new { definitionCode = "COMPLAINT", referenceType = " ", referenceId = Guid.NewGuid() }, student);
            Assert.Equal(400, invalid.Code);
        }

        [Fact]
        public async Task Two_parallel_approvals_of_the_same_step_let_exactly_one_win()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("wfs", RoleNames.Student);
            var head = await TokenAsync("wfh", RoleNames.DepartmentHead, ScopeType.Department, department);

            var id = (await StartAsync(student, "LEAVE_REQUEST", department)).GuidOf("id");

            var results = await Task.WhenAll(ActAsync(head, id, "approve"), ActAsync(head, id, "approve"));

            Assert.Single(results, r => r.Code == 200);
            Assert.Single(results, r => r.Code == 403 || r.Code == 409);

            var current = await Client.GetAsync($"/api/v1/workflows/{id}", student);
            Assert.Equal(2, current.Prop("currentStepOrder").GetInt32());
            Assert.Equal(1, current.Prop("approvals").GetArrayLength());
        }
    }
}
