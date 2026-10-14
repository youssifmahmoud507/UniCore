using System.Text.Json;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class StudentApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        // The create call answers with { student: { summary, ... }, accountProvisioned, accountErrorCode }.
        private static JsonElement OutcomeSummary(ApiResponse response)
            => response.Body.GetProperty("student").GetProperty("summary");

        // Every other student endpoint answers with the detail itself: { summary, programHistory, statusHistory }.
        private static JsonElement SummaryOf(ApiResponse response) => response.Body.GetProperty("summary");

        private static Guid IdOf(JsonElement summary)
            => Guid.Parse(summary.GetProperty("id").GetString() ?? string.Empty);

        private async Task<(AdmitSpec Spec, Guid Id)> AdmitAsync(string? email = null, bool createAccount = false, string? code = null)
        {
            var spec = new AdmitSpec(code, email, createAccount);
            var admin = await _api.AdminTokenAsync();

            var created = await Client.PostAsync("/api/v1/students", spec.Body, admin);
            Assert.Equal(201, created.Code);

            return (spec, IdOf(OutcomeSummary(created)));
        }

        [Fact]
        public async Task Admin_admits_a_student_with_a_login_account()
        {
            var email = $"{TestApi.NewName("stu")}@uni.test";
            var spec = new AdmitSpec(null, email, createAccount: true);
            var admin = await _api.AdminTokenAsync();
            var skip = _api.Emails.Count;

            var created = await Client.PostAsync("/api/v1/students", spec.Body, admin);

            Assert.Equal(201, created.Code);
            var summary = OutcomeSummary(created);
            Assert.Equal(spec.Number(1), summary.GetProperty("studentNumber").GetString());
            Assert.Equal("Active", summary.GetProperty("status").GetString());
            Assert.Equal(spec.Program.ToString(), summary.GetProperty("currentProgramId").GetString());
            Assert.Equal(JsonValueKind.String, summary.GetProperty("userId").ValueKind);
            Assert.True(created.Prop("accountProvisioned").GetBoolean());
            Assert.Equal(JsonValueKind.Null, created.Prop("accountErrorCode").ValueKind);
            Assert.Equal(1, created.Prop("student").GetProperty("programHistory").GetArrayLength());
            Assert.Equal(0, created.Prop("student").GetProperty("statusHistory").GetArrayLength());

            var welcome = await _api.Emails.WaitForAsync(
                m => m.To == email && m.Subject.Contains("account was created", StringComparison.OrdinalIgnoreCase),
                skip, TimeSpan.FromSeconds(10));

            Assert.NotNull(welcome);
            Assert.Contains(spec.Number(1), welcome.TextBody);
        }

        [Fact]
        public async Task Student_numbers_are_unique_and_sequential_under_parallel_requests()
        {
            var admin = await _api.AdminTokenAsync();
            var template = new AdmitSpec();
            var specs = Enumerable.Range(0, 8).Select(_ => template.WithNewPerson()).ToList();

            var responses = await Task.WhenAll(
                specs.Select(spec => Client.PostAsync("/api/v1/students", spec.Body, admin)));

            Assert.All(responses, r => Assert.Equal(201, r.Code));

            var numbers = responses
                .Select(r => OutcomeSummary(r).GetProperty("studentNumber").GetString())
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            var expected = Enumerable.Range(1, 8).Select(i => template.Number(i)).ToList();

            Assert.Equal(expected, numbers);
        }

        [Fact]
        public async Task The_same_person_cannot_be_admitted_twice_even_in_parallel()
        {
            var admin = await _api.AdminTokenAsync();

            var sequential = new AdmitSpec();
            var first = await Client.PostAsync("/api/v1/students", sequential.Body, admin);
            var second = await Client.PostAsync("/api/v1/students", sequential.Body, admin);

            Assert.Equal(201, first.Code);
            Assert.Equal(409, second.Code);
            Assert.Equal("STUDENT_ALREADY_EXISTS", second.ErrorCode);

            var parallel = new AdmitSpec();
            var both = await Task.WhenAll(
                Client.PostAsync("/api/v1/students", parallel.Body, admin),
                Client.PostAsync("/api/v1/students", parallel.Body, admin));

            Assert.Single(both, r => r.Code == 201);
            var loser = Assert.Single(both, r => r.Code == 409);
            Assert.Contains(loser.ErrorCode, new[] { "STUDENT_ALREADY_EXISTS", "DUPLICATE_RESOURCE" });
        }

        [Fact]
        public async Task Invalid_admissions_are_rejected()
        {
            var admin = await _api.AdminTokenAsync();

            var empty = await Client.PostAsync("/api/v1/students", new { createAccount = false }, admin);
            Assert.Equal(400, empty.Code);
            Assert.Equal("STUDENT_DATA_INVALID", empty.ErrorCode);

            var accountWithoutEmail = await Client.PostAsync("/api/v1/students", new AdmitSpec(null, null, true).Body, admin);
            Assert.Equal(400, accountWithoutEmail.Code);
            Assert.Equal("USER_EMAIL_INVALID", accountWithoutEmail.ErrorCode);

            var badCode = await Client.PostAsync("/api/v1/students", new AdmitSpec("!!").Body, admin);
            Assert.Equal(400, badCode.Code);
            Assert.Equal("STUDENT_DATA_INVALID", badCode.ErrorCode);

            var spec = new AdmitSpec();
            var badYear = await Client.PostAsync(
                "/api/v1/students",
                new
                {
                    personProfileId = spec.Person,
                    admissionDate = spec.Admission,
                    academicProgramId = spec.Program,
                    programCode = spec.Code,
                    studyPlanVersionId = spec.Plan,
                    admissionAcademicYearId = spec.AcademicYear,
                    admissionYear = 1999,
                    createAccount = false
                },
                admin);
            Assert.Equal("STUDENT_DATA_INVALID", badYear.ErrorCode);

            var anonymous = await Client.PostAsync("/api/v1/students", new AdmitSpec().Body);
            Assert.Equal(401, anonymous.Code);
        }

        [Fact]
        public async Task The_student_sets_a_password_and_reads_only_their_own_record()
        {
            var email = $"{TestApi.NewName("self")}@uni.test";
            var (spec, id) = await AdmitAsync(email, createAccount: true);

            await _api.SetPasswordAsync(email, TestApi.DefaultPassword);

            // The student number is the user name.
            var login = await _api.LoginAsync(spec.Number(1), TestApi.DefaultPassword);
            Assert.Equal(200, login.Code);
            var token = login.Str("accessToken");

            var me = await Client.GetAsync("/api/v1/me", token);
            Assert.Contains(RoleNames.Student, me.Prop("roles").EnumerateArray().Select(e => e.GetString()));

            var mine = await Client.GetAsync("/api/v1/students/me", token);
            Assert.Equal(200, mine.Code);
            Assert.Equal(spec.Number(1), SummaryOf(mine).GetProperty("studentNumber").GetString());
            Assert.Equal(id, IdOf(SummaryOf(mine)));

            var list = await Client.GetAsync("/api/v1/students", token);
            Assert.Equal(403, list.Code);
            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", list.ErrorCode);

            var byId = await Client.GetAsync($"/api/v1/students/{id}", token);
            Assert.Equal(403, byId.Code);

            var create = await Client.PostAsync("/api/v1/students", new AdmitSpec().Body, token);
            Assert.Equal(403, create.Code);

            var noStudentRecord = await Client.GetAsync("/api/v1/students/me", await _api.AdminTokenAsync());
            Assert.Equal(404, noStudentRecord.Code);
            Assert.Equal("STUDENT_NOT_FOUND", noStudentRecord.ErrorCode);
        }

        [Fact]
        public async Task Status_workflow_follows_the_rules_and_keeps_the_history()
        {
            var (_, id) = await AdmitAsync();
            var admin = await _api.AdminTokenAsync();
            var url = $"/api/v1/students/{id}/status";
            var today = AdmitSpec.Today;

            var noReason = await Client.PostAsync(url, new { newStatus = StudentAcademicStatus.OnLeave, effectiveDate = today }, admin);
            Assert.Equal(400, noReason.Code);
            Assert.Equal("STUDENT_STATUS_REASON_REQUIRED", noReason.ErrorCode);

            var leave = await Client.PostAsync(
                url, new { newStatus = StudentAcademicStatus.OnLeave, effectiveDate = today, reason = "family reasons" }, admin);
            Assert.Equal(200, leave.Code);
            Assert.Equal("OnLeave", SummaryOf(leave).GetProperty("status").GetString());
            Assert.Equal(1, leave.Prop("statusHistory").GetArrayLength());

            var back = await Client.PostAsync(url, new { newStatus = StudentAcademicStatus.Active, effectiveDate = today }, admin);
            Assert.Equal(200, back.Code);
            Assert.Equal("Active", SummaryOf(back).GetProperty("status").GetString());

            var unchanged = await Client.PostAsync(url, new { newStatus = StudentAcademicStatus.Active, effectiveDate = today }, admin);
            Assert.Equal(422, unchanged.Code);
            Assert.Equal("STUDENT_STATUS_UNCHANGED", unchanged.ErrorCode);

            var graduated = await Client.PostAsync(url, new { newStatus = StudentAcademicStatus.Graduated, effectiveDate = today }, admin);
            Assert.Equal(200, graduated.Code);
            Assert.Equal(today.ToString("yyyy-MM-dd"), SummaryOf(graduated).GetProperty("graduationDate").GetString());
            Assert.Equal(3, graduated.Prop("statusHistory").GetArrayLength());
            var program = graduated.Prop("programHistory").EnumerateArray().First();
            Assert.Equal("Completed", program.GetProperty("status").GetString());

            var afterGraduation = await Client.PostAsync(url, new { newStatus = StudentAcademicStatus.Active, effectiveDate = today }, admin);
            Assert.Equal(422, afterGraduation.Code);
            Assert.Equal("STUDENT_STATUS_TRANSITION_INVALID", afterGraduation.ErrorCode);

            var missingFields = await Client.PostAsync(url, new { }, admin);
            Assert.Equal("STUDENT_DATA_INVALID", missingFields.ErrorCode);

            var unknownStudent = await Client.PostAsync(
                $"/api/v1/students/{Guid.NewGuid()}/status",
                new { newStatus = StudentAcademicStatus.OnLeave, effectiveDate = today, reason = "x" }, admin);
            Assert.Equal(404, unknownStudent.Code);
            Assert.Equal("STUDENT_NOT_FOUND", unknownStudent.ErrorCode);
        }

        [Fact]
        public async Task Changing_the_program_keeps_the_old_record_and_opens_a_new_one()
        {
            var (spec, id) = await AdmitAsync();
            var admin = await _api.AdminTokenAsync();
            var url = $"/api/v1/students/{id}/program";
            var today = AdmitSpec.Today;
            var newProgram = Guid.NewGuid();
            var newPlan = Guid.NewGuid();

            var noReason = await Client.PostAsync(
                url, new { academicProgramId = newProgram, studyPlanVersionId = newPlan, startDate = today }, admin);
            Assert.Equal("STUDENT_PROGRAM_CHANGE_REASON_REQUIRED", noReason.ErrorCode);

            var unchanged = await Client.PostAsync(
                url, new { academicProgramId = spec.Program, studyPlanVersionId = spec.Plan, startDate = today, reason = "x" }, admin);
            Assert.Equal(422, unchanged.Code);
            Assert.Equal("STUDENT_PROGRAM_UNCHANGED", unchanged.ErrorCode);

            var missing = await Client.PostAsync(url, new { }, admin);
            Assert.Equal("STUDENT_DATA_INVALID", missing.ErrorCode);

            var changed = await Client.PostAsync(
                url, new { academicProgramId = newProgram, studyPlanVersionId = newPlan, startDate = today, reason = "transfer" }, admin);
            Assert.Equal(200, changed.Code);

            var history = changed.Prop("programHistory").EnumerateArray().ToList();
            Assert.Equal(2, history.Count);
            Assert.Equal("Changed", history[0].GetProperty("status").GetString());
            Assert.Equal(today.ToString("yyyy-MM-dd"), history[0].GetProperty("endDate").GetString());
            Assert.Equal("Active", history[1].GetProperty("status").GetString());
            Assert.Equal("transfer", history[1].GetProperty("reason").GetString());
            Assert.Equal(newProgram.ToString(), SummaryOf(changed).GetProperty("currentProgramId").GetString());
            Assert.Equal(newPlan.ToString(), SummaryOf(changed).GetProperty("currentStudyPlanVersionId").GetString());

            // Only an active student can change program.
            var leave = await Client.PostAsync(
                $"/api/v1/students/{id}/status",
                new { newStatus = StudentAcademicStatus.OnLeave, effectiveDate = today, reason = "x" }, admin);
            Assert.Equal(200, leave.Code);

            var whileOnLeave = await Client.PostAsync(
                url, new { academicProgramId = Guid.NewGuid(), studyPlanVersionId = Guid.NewGuid(), startDate = today, reason = "x" }, admin);
            Assert.Equal(422, whileOnLeave.Code);
            Assert.Equal("STUDENT_NOT_ACTIVE", whileOnLeave.ErrorCode);
        }

        [Fact]
        public async Task The_academic_profile_can_be_updated()
        {
            var (_, id) = await AdmitAsync();
            var admin = await _api.AdminTokenAsync();
            var url = $"/api/v1/students/{id}/academic-profile";
            var level = Guid.NewGuid();

            var standing = await Client.PatchAsync(url, new { academicStanding = AcademicStanding.Probation }, admin);
            Assert.Equal(200, standing.Code);
            Assert.Equal("Probation", SummaryOf(standing).GetProperty("standing").GetString());

            var levelChange = await Client.PatchAsync(url, new { academicLevelId = level }, admin);
            Assert.Equal(200, levelChange.Code);
            Assert.Equal(level.ToString(), SummaryOf(levelChange).GetProperty("currentAcademicLevelId").GetString());
            Assert.Equal("Probation", SummaryOf(levelChange).GetProperty("standing").GetString());

            var empty = await Client.PatchAsync(url, new { }, admin);
            Assert.Equal(400, empty.Code);
            Assert.Equal("STUDENT_DATA_INVALID", empty.ErrorCode);
        }

        [Fact]
        public async Task Listing_supports_paging_search_and_status_filter()
        {
            var code = AdmitSpec.NewCode();
            var first = await AdmitAsync(code: code);
            await AdmitAsync(code: code);
            await AdmitAsync(code: code);
            var admin = await _api.AdminTokenAsync();
            var prefix = $"{first.Spec.Admission.Year}{code}";

            var leave = await Client.PostAsync(
                $"/api/v1/students/{first.Id}/status",
                new { newStatus = StudentAcademicStatus.OnLeave, effectiveDate = AdmitSpec.Today, reason = "x" }, admin);
            Assert.Equal(200, leave.Code);

            var page1 = await Client.GetAsync($"/api/v1/students?search={prefix}&page=1&pageSize=2", admin);
            Assert.Equal(200, page1.Code);
            Assert.Equal(3, page1.Prop("totalCount").GetInt32());
            Assert.Equal(2, page1.Prop("items").GetArrayLength());
            Assert.True(page1.Prop("hasNext").GetBoolean());
            Assert.False(page1.Prop("hasPrevious").GetBoolean());
            Assert.Equal(
                first.Spec.Number(1),
                page1.Prop("items").EnumerateArray().First().GetProperty("studentNumber").GetString());

            var page2 = await Client.GetAsync($"/api/v1/students?search={prefix}&page=2&pageSize=2", admin);
            Assert.Equal(1, page2.Prop("items").GetArrayLength());
            Assert.False(page2.Prop("hasNext").GetBoolean());

            var onLeave = await Client.GetAsync($"/api/v1/students?search={prefix}&status=OnLeave", admin);
            Assert.Equal(1, onLeave.Prop("totalCount").GetInt32());

            var lowerCase = await Client.GetAsync($"/api/v1/students?search={prefix.ToLowerInvariant()}", admin);
            Assert.Equal(3, lowerCase.Prop("totalCount").GetInt32());
        }

        [Fact]
        public async Task A_registrar_with_a_global_scope_can_work_but_a_dean_is_denied_for_now()
        {
            var registrar = await _api.CreateUserAsync("registrar", RoleNames.Registrar, ScopeType.Global);
            var token = await _api.LoginTokenAsync(registrar.UserName, registrar.Password);
            var spec = new AdmitSpec();

            var created = await Client.PostAsync("/api/v1/students", spec.Body, token);
            Assert.Equal(201, created.Code);
            var id = IdOf(OutcomeSummary(created));

            var list = await Client.GetAsync($"/api/v1/students?search={spec.Admission.Year}{spec.Code}", token);
            Assert.Equal(200, list.Code);

            var leave = await Client.PostAsync(
                $"/api/v1/students/{id}/status",
                new { newStatus = StudentAcademicStatus.OnLeave, effectiveDate = AdmitSpec.Today, reason = "x" }, token);
            Assert.Equal(200, leave.Code);

            // TEMPORARY rule: until the program -> department -> faculty hierarchy exists (Developer 1),
            // staff access to students needs a Global scope. Change this test when that rule changes.
            var dean = await _api.CreateUserAsync("deanx", RoleNames.Dean, ScopeType.Faculty, Guid.NewGuid());
            var denied = await Client.GetAsync("/api/v1/students", await _api.LoginTokenAsync(dean.UserName, dean.Password));
            Assert.Equal(403, denied.Code);
            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", denied.ErrorCode);
        }

        [Fact]
        public async Task The_account_can_be_created_later_for_a_student_without_one()
        {
            var (_, id) = await AdmitAsync(createAccount: false);
            var admin = await _api.AdminTokenAsync();
            var url = $"/api/v1/students/{id}/account";

            var detail = await Client.GetAsync($"/api/v1/students/{id}", admin);
            Assert.Equal(JsonValueKind.Null, SummaryOf(detail).GetProperty("userId").ValueKind);

            var bad = await Client.PostAsync(url, new { email = "nope" }, admin);
            Assert.Equal(400, bad.Code);
            Assert.Equal("USER_EMAIL_INVALID", bad.ErrorCode);

            var email = $"{TestApi.NewName("later")}@uni.test";
            var skip = _api.Emails.Count;
            var ok = await Client.PostAsync(url, new { email }, admin);
            Assert.Equal(200, ok.Code);
            Assert.Equal(JsonValueKind.String, SummaryOf(ok).GetProperty("userId").ValueKind);

            var mail = await _api.Emails.WaitForAsync(
                m => m.To == email && m.Subject.Contains("account was created", StringComparison.OrdinalIgnoreCase),
                skip, TimeSpan.FromSeconds(10));
            Assert.NotNull(mail);

            var second = await Client.PostAsync(url, new { email = $"{TestApi.NewName("again")}@uni.test" }, admin);
            Assert.Equal(409, second.Code);
            Assert.Equal("STUDENT_ACCOUNT_ALREADY_LINKED", second.ErrorCode);
        }

        [Fact]
        public async Task A_duplicate_email_does_not_fail_the_admission_and_can_be_fixed_later()
        {
            var email = $"{TestApi.NewName("shared")}@uni.test";
            await AdmitAsync(email, createAccount: true);
            var admin = await _api.AdminTokenAsync();

            var created = await Client.PostAsync("/api/v1/students", new AdmitSpec(null, email, true).Body, admin);

            Assert.Equal(201, created.Code);
            Assert.False(created.Prop("accountProvisioned").GetBoolean());
            Assert.Equal("USER_ALREADY_EXISTS", created.Prop("accountErrorCode").GetString());
            Assert.Equal(JsonValueKind.Null, OutcomeSummary(created).GetProperty("userId").ValueKind);

            var id = IdOf(OutcomeSummary(created));
            var retry = await Client.PostAsync(
                $"/api/v1/students/{id}/account", new { email = $"{TestApi.NewName("fixed")}@uni.test" }, admin);
            Assert.Equal(200, retry.Code);
            Assert.Equal(JsonValueKind.String, SummaryOf(retry).GetProperty("userId").ValueKind);
        }

        [Fact]
        public async Task Parallel_status_changes_never_both_win()
        {
            var (_, id) = await AdmitAsync();
            var admin = await _api.AdminTokenAsync();
            var url = $"/api/v1/students/{id}/status";
            var today = AdmitSpec.Today;

            var results = await Task.WhenAll(
                Client.PostAsync(url, new { newStatus = StudentAcademicStatus.OnLeave, effectiveDate = today, reason = "a" }, admin),
                Client.PostAsync(url, new { newStatus = StudentAcademicStatus.Suspended, effectiveDate = today, reason = "b" }, admin));

            Assert.Single(results, r => r.Code == 200);
            var loser = Assert.Single(results, r => r.Code != 200);

            // 409: lost the race on the row version. 422: the other change had already finished.
            Assert.Contains(loser.Code, new[] { 409, 422 });

            var detail = await Client.GetAsync($"/api/v1/students/{id}", admin);
            Assert.Equal(1, detail.Prop("statusHistory").GetArrayLength());
        }
    }

}
