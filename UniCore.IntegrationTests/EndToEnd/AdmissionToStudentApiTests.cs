using Microsoft.Extensions.DependencyInjection;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // An accepted application becomes a student by itself (person profile, student on the active plan, login account).
    [Collection(ApiCollection.Name)]
    public sealed class AdmissionToStudentApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private OutboxProcessor Processor => fixture.Services.GetRequiredService<OutboxProcessor>();

        private static string Short() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        // An application for the program, accepted by the registrar.
        private async Task<(Guid ApplicationId, string Email)> AcceptedApplicationAsync(string admin, Guid programId, Guid yearId)
        {
            var email = $"{Short().ToLowerInvariant()}@mail.test";
            var applicant = await Client.PostAsync("/api/v1/applicants", new { fullName = $"Mona Ahmed {Short()}", email, phone = "0100000000", dateOfBirth = new DateOnly(2005, 5, 5) }, admin);
            Assert.Equal(201, applicant.Code);

            Assert.Equal(201, (await Client.PostAsync("/api/v1/admission-requirements", new { academicProgramId = programId, code = $"M{Short()}", name = "Certificate", description = "x", isMandatory = true }, admin)).Code);

            var created = await Client.PostAsync("/api/v1/admission-applications", new { applicantId = applicant.GuidOf("id"), academicProgramId = programId, academicYearId = yearId }, admin);
            Assert.Equal(201, created.Code);
            var applicationId = created.Prop("application").GetProperty("id").GetGuid();
            var path = $"/api/v1/admission-applications/{applicationId}";

            var requirements = created.Prop("requirements").EnumerateArray().Where(r => r.GetProperty("isMandatory").GetBoolean()).Select(r => r.GetProperty("id").GetGuid()).ToList();
            foreach (var requirement in requirements)
            {
                Assert.Equal(200, (await Client.PutAsync($"{path}/requirements/{requirement}/document", new { documentId = Guid.NewGuid() }, admin)).Code);
            }

            Assert.Equal(200, (await Client.PostAsync($"{path}/submit", null, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"{path}/start-review", null, admin)).Code);

            foreach (var requirement in requirements)
            {
                Assert.Equal(200, (await Client.PostAsync($"{path}/requirements/{requirement}/review", new { approve = true, note = "ok" }, admin)).Code);
            }

            Assert.Equal(200, (await Client.PostAsync($"{path}/accept", new { reason = "Good grades" }, admin)).Code);

            return (applicationId, email);
        }

        private async Task<int> AuditCountEventuallyAsync(string admin, string action, Guid applicationId)
        {
            var count = 0;
            var deadline = DateTime.UtcNow.AddSeconds(25);

            while (count == 0 && DateTime.UtcNow < deadline)
            {
                await Processor.ProcessBatchAsync(200);
                var audit = await Client.GetAsync($"/api/v1/audit-logs?action={action}&entityId={applicationId}", admin);
                Assert.Equal(200, audit.Code);
                count = audit.Prop("totalCount").GetInt32();

                if (count == 0)
                {
                    await Task.Delay(300);
                }
            }

            return count;
        }

        [Fact]
        public async Task Journey_an_accepted_applicant_becomes_a_student_with_an_account_and_only_once()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var (applicationId, email) = await AcceptedApplicationAsync(admin, setup.ProgramId, setup.Calendar.YearId);

            Assert.Equal(1, await AuditCountEventuallyAsync(admin, "ADMISSION_STUDENT_CREATED", applicationId));

            // The person profile made from the applicant.
            var people = await Client.GetAsync($"/api/v1/people?search={email}", admin);
            Assert.Equal(200, people.Code);
            var person = people.Prop("items").EnumerateArray().Single(p => p.GetProperty("email").GetString() == email);
            Assert.Equal("Mona", person.GetProperty("firstName").GetString());
            var personId = person.GetProperty("id").GetGuid();

            // The student: on the program's active plan, with a login account (the number starts with year + program code).
            var year = await Client.GetAsync($"/api/v1/academic-years/{setup.Calendar.YearId}", admin);
            Assert.Equal(200, year.Code);
            var prefix = $"{DateOnly.Parse(year.Str("startDate")).Year}{setup.ProgramCode}";

            var students = await Client.GetAsync($"/api/v1/students?search={prefix}&pageSize=100", admin);
            Assert.Equal(200, students.Code);
            var student = students.Prop("items").EnumerateArray().Single(s => s.GetProperty("personProfileId").GetGuid() == personId);

            Assert.Equal("Active", student.GetProperty("status").GetString());
            Assert.Equal(setup.ProgramId, student.GetProperty("currentProgramId").GetGuid());
            Assert.Equal(setup.PlanVersionId, student.GetProperty("currentStudyPlanVersionId").GetGuid());
            Assert.NotEqual(Guid.Empty, student.GetProperty("userId").GetGuid());

            // Handled again (at-least-once delivery): nothing is created twice.
            await Processor.ProcessBatchAsync(200);
            students = await Client.GetAsync($"/api/v1/students?search={prefix}&pageSize=100", admin);
            Assert.Single(students.Prop("items").EnumerateArray(), s => s.GetProperty("personProfileId").GetGuid() == personId);
            Assert.Equal(1, (await Client.GetAsync($"/api/v1/audit-logs?action=ADMISSION_STUDENT_CREATED&entityId={applicationId}", admin)).Prop("totalCount").GetInt32());
        }

        [Fact]
        public async Task Journey_a_program_without_an_active_study_plan_leaves_the_applicant_unadmitted_and_says_why()
        {
            var admin = await _api.AdminTokenAsync();
            var calendar = await AcademicScenario.EnsureCalendarAsync(_api, admin);
            var (_, programId, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var (applicationId, email) = await AcceptedApplicationAsync(admin, programId, calendar.YearId);

            Assert.Equal(1, await AuditCountEventuallyAsync(admin, "ADMISSION_STUDENT_FAILED", applicationId));
            Assert.Equal(0, (await Client.GetAsync($"/api/v1/audit-logs?action=ADMISSION_STUDENT_CREATED&entityId={applicationId}", admin)).Prop("totalCount").GetInt32());

            // No person was made for them either, so the registrar can still admit them by hand later.
            var people = await Client.GetAsync($"/api/v1/people?search={email}", admin);
            Assert.Equal(200, people.Code);
            Assert.Empty(people.Prop("items").EnumerateArray().Where(p => p.GetProperty("email").GetString() == email));
        }
    }
}
