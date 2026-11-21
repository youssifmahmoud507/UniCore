using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Applicant -> application -> documents -> review -> decision, through the real API.
    // The deadline and the document check are replaced in UniCoreApiFactory (see FixedAdmissionDeadlineProvider).
    [Collection(ApiCollection.Name)]
    public sealed class AdmissionsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private sealed record Application(Guid Id, Guid ProgramId, Guid ApplicantId, Guid YearId, IReadOnlyList<Guid> RequirementIds);

        private static string Short() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        private async Task<Guid> NewApplicantAsync(string admin, string? email = null)
        {
            var response = await Client.PostAsync("/api/v1/applicants", new { fullName = "Test Applicant", email = email ?? $"{Short().ToLowerInvariant()}@mail.test", dateOfBirth = new DateOnly(2005, 5, 5) }, admin);
            Assert.Equal(201, response.Code);

            return response.GuidOf("id");
        }

        // A program with `mandatory` mandatory requirements and one optional one, and a Draft application for it.
        private async Task<Application> NewApplicationAsync(string admin, int mandatory = 1)
        {
            var calendar = await AcademicScenario.EnsureCalendarAsync(_api, admin);
            var (_, programId, _) = await AcademicScenario.CreateProgramAsync(_api, admin);

            for (var i = 0; i < mandatory; i++)
            {
                var required = await Client.PostAsync("/api/v1/admission-requirements", new { academicProgramId = programId, code = $"M{Short()}", name = $"Mandatory {i}", description = "x", isMandatory = true }, admin);
                Assert.Equal(201, required.Code);
            }

            var optional = await Client.PostAsync("/api/v1/admission-requirements", new { academicProgramId = programId, code = $"O{Short()}", name = "Optional", description = "x", isMandatory = false }, admin);
            Assert.Equal(201, optional.Code);

            var applicantId = await NewApplicantAsync(admin);

            var created = await Client.PostAsync("/api/v1/admission-applications", new { applicantId, academicProgramId = programId, academicYearId = calendar.YearId }, admin);
            Assert.Equal(201, created.Code);

            var ids = created.Prop("requirements").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToList();

            return new Application(created.Prop("application").GetProperty("id").GetGuid(), programId, applicantId, calendar.YearId, ids);
        }

        private Task<ApiResponse> AttachAsync(string admin, Application application, Guid requirementId)
            => Client.PutAsync($"/api/v1/admission-applications/{application.Id}/requirements/{requirementId}/document", new { documentId = Guid.NewGuid() }, admin);

        private async Task<ApiResponse> GetAsync(string admin, Application application)
            => await Client.GetAsync($"/api/v1/admission-applications/{application.Id}", admin);

        private Task<ApiResponse> StepAsync(string admin, Application application, string step, object? body = null)
            => Client.PostAsync($"/api/v1/admission-applications/{application.Id}/{step}", body, admin);

        // Attaches a document to the mandatory requirements, submits, and starts the review.
        private async Task<Application> UnderReviewAsync(string admin, int mandatory = 1)
        {
            var application = await NewApplicationAsync(admin, mandatory);

            var detail = await GetAsync(admin, application);
            var mandatoryIds = detail.Prop("requirements").EnumerateArray().Where(r => r.GetProperty("isMandatory").GetBoolean()).Select(r => r.GetProperty("id").GetGuid()).ToList();
            Assert.Equal(mandatory, mandatoryIds.Count);

            foreach (var id in mandatoryIds)
            {
                Assert.Equal(200, (await AttachAsync(admin, application, id)).Code);
            }

            Assert.Equal(200, (await StepAsync(admin, application, "submit")).Code);
            Assert.Equal(200, (await StepAsync(admin, application, "start-review")).Code);

            return application;
        }

        private async Task ApproveMandatoryAsync(string admin, Application application)
        {
            var detail = await GetAsync(admin, application);
            foreach (var requirement in detail.Prop("requirements").EnumerateArray().Where(r => r.GetProperty("isMandatory").GetBoolean()))
            {
                var reviewed = await Client.PostAsync($"/api/v1/admission-applications/{application.Id}/requirements/{requirement.GetProperty("id").GetGuid()}/review", new { approve = true, note = "Looks fine" }, admin);
                Assert.Equal(200, reviewed.Code);
            }
        }

        [Fact]
        public async Task An_applicant_email_is_unique()
        {
            var admin = await _api.AdminTokenAsync();
            var email = $"{Short().ToLowerInvariant()}@mail.test";

            await NewApplicantAsync(admin, email);

            var duplicate = await Client.PostAsync("/api/v1/applicants", new { fullName = "Other Person", email, dateOfBirth = new DateOnly(2004, 4, 4) }, admin);

            Assert.Equal(409, duplicate.Code);
            Assert.Equal("APPLICANT_EMAIL_EXISTS", duplicate.ErrorCode);
        }

        [Fact]
        public async Task A_new_application_copies_the_active_requirements_of_the_program()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await NewApplicationAsync(admin, mandatory: 2);

            var detail = await GetAsync(admin, application);

            Assert.Equal(200, detail.Code);
            Assert.Equal("Draft", detail.Prop("application").GetProperty("status").GetString());
            Assert.Equal(3, detail.Prop("requirements").GetArrayLength());
            Assert.Equal(2, detail.Prop("requirements").EnumerateArray().Count(r => r.GetProperty("isMandatory").GetBoolean()));
            Assert.All(detail.Prop("requirements").EnumerateArray(), r => Assert.Equal("Pending", r.GetProperty("reviewStatus").GetString()));
        }

        [Fact]
        public async Task The_same_applicant_cannot_apply_twice_to_the_same_program_and_year()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await NewApplicationAsync(admin);

            var duplicate = await Client.PostAsync("/api/v1/admission-applications", new { applicantId = application.ApplicantId, academicProgramId = application.ProgramId, academicYearId = application.YearId }, admin);

            Assert.Equal(409, duplicate.Code);
            Assert.Equal("ADMISSION_DUPLICATE_APPLICATION", duplicate.ErrorCode);
        }

        [Fact]
        public async Task An_application_needs_a_document_for_every_mandatory_requirement_before_it_is_submitted()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await NewApplicationAsync(admin, mandatory: 2);

            var early = await StepAsync(admin, application, "submit");
            Assert.Equal(422, early.Code);
            Assert.Equal("ADMISSION_MANDATORY_REQUIREMENT_MISSING", early.ErrorCode);

            var detail = await GetAsync(admin, application);
            foreach (var id in detail.Prop("requirements").EnumerateArray().Where(r => r.GetProperty("isMandatory").GetBoolean()).Select(r => r.GetProperty("id").GetGuid()))
            {
                Assert.Equal(200, (await AttachAsync(admin, application, id)).Code);
            }

            var submitted = await StepAsync(admin, application, "submit");
            Assert.Equal(200, submitted.Code);
            Assert.Equal("Submitted", submitted.Prop("application").GetProperty("status").GetString());
        }

        [Fact]
        public async Task A_reviewed_application_is_accepted_only_when_every_mandatory_requirement_is_approved()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await UnderReviewAsync(admin);

            var early = await StepAsync(admin, application, "accept", new { reason = "Good grades" });
            Assert.Equal(422, early.Code);
            Assert.Equal("ADMISSION_MANDATORY_REQUIREMENT_NOT_APPROVED", early.ErrorCode);

            await ApproveMandatoryAsync(admin, application);

            var accepted = await StepAsync(admin, application, "accept", new { reason = "Good grades" });
            Assert.Equal(200, accepted.Code);
            Assert.Equal("Accepted", accepted.Prop("application").GetProperty("status").GetString());
            Assert.Equal(1, accepted.Prop("decisions").GetArrayLength());

            var again = await StepAsync(admin, application, "accept", new { reason = "Again" });
            Assert.Equal(422, again.Code);
            Assert.Equal("ADMISSION_INVALID_STATE", again.ErrorCode);
        }

        [Fact]
        public async Task A_conditional_acceptance_becomes_an_acceptance_when_the_conditions_are_met()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await UnderReviewAsync(admin);

            var conditional = await StepAsync(admin, application, "conditionally-accept", new { reason = "Pending papers" });
            Assert.Equal(200, conditional.Code);
            Assert.Equal("ConditionallyAccepted", conditional.Prop("application").GetProperty("status").GetString());

            var early = await StepAsync(admin, application, "complete-conditions", new { reason = "Done" });
            Assert.Equal(422, early.Code);
            Assert.Equal("ADMISSION_CONDITIONS_PENDING", early.ErrorCode);

            await ApproveMandatoryAsync(admin, application);

            var completed = await StepAsync(admin, application, "complete-conditions", new { reason = "Done" });
            Assert.Equal(200, completed.Code);
            Assert.Equal("Accepted", completed.Prop("application").GetProperty("status").GetString());
        }

        [Fact]
        public async Task A_conditional_acceptance_is_refused_when_there_is_nothing_pending()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await UnderReviewAsync(admin);
            await ApproveMandatoryAsync(admin, application);

            var response = await StepAsync(admin, application, "conditionally-accept", new { reason = "Why not" });

            Assert.Equal(422, response.Code);
            Assert.Equal("ADMISSION_NO_CONDITIONS_PENDING", response.ErrorCode);
        }

        [Fact]
        public async Task A_rejection_needs_a_reason()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await UnderReviewAsync(admin);

            var without = await StepAsync(admin, application, "reject", new { reason = "" });
            Assert.Equal(400, without.Code);
            Assert.Equal("ADMISSION_REASON_REQUIRED", without.ErrorCode);

            var rejected = await StepAsync(admin, application, "reject", new { reason = "Missing prerequisites" });
            Assert.Equal(200, rejected.Code);
            Assert.Equal("Rejected", rejected.Prop("application").GetProperty("status").GetString());
        }

        [Fact]
        public async Task A_withdrawn_application_cannot_be_submitted_and_a_draft_cannot_be_decided()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await NewApplicationAsync(admin);

            var early = await StepAsync(admin, application, "accept", new { reason = "Too early" });
            Assert.Equal(422, early.Code);
            Assert.Equal("ADMISSION_INVALID_STATE", early.ErrorCode);

            var withdrawn = await StepAsync(admin, application, "withdraw");
            Assert.Equal(200, withdrawn.Code);
            Assert.Equal("Withdrawn", withdrawn.Prop("application").GetProperty("status").GetString());

            var submit = await StepAsync(admin, application, "submit");
            Assert.Equal(422, submit.Code);
            Assert.Equal("ADMISSION_INVALID_STATE", submit.ErrorCode);
        }

        [Fact]
        public async Task A_user_without_admission_permissions_is_refused()
        {
            var admin = await _api.AdminTokenAsync();
            var application = await NewApplicationAsync(admin);

            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);

            var read = await Client.GetAsync($"/api/v1/admission-applications/{application.Id}", token);
            Assert.Equal(403, read.Code);

            var decide = await Client.PostAsync($"/api/v1/admission-applications/{application.Id}/accept", new { reason = "Let me in" }, token);
            Assert.Equal(403, decide.Code);
        }
    }
}
