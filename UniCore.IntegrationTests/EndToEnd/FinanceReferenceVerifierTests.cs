using Microsoft.Extensions.DependencyInjection;
using UniCore.Infrastructure.Modules.Finance;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // The API tests use the permissive verifier. Here the REAL one runs against data created through Developer 1's APIs.
    [Collection(ApiCollection.Name)]
    public sealed class FinanceReferenceVerifierTests(ApiFixture fixture)
    {
        private ApiClient Client => fixture.Api.Client;

        private async Task<(Guid University, Guid Program)> CreateProgramAsync(string admin)
        {
            var code = $"U{Guid.NewGuid():N}"[..8].ToUpperInvariant();

            var university = await Client.PostAsync("/api/v1/universities", new { code, name = $"University {code}" }, admin);
            Assert.Equal(201, university.Code);
            var universityId = university.GuidOf("id");

            var faculty = await Client.PostAsync("/api/v1/faculties", new { universityId, code = $"F{code[1..]}", name = "Faculty" }, admin);
            Assert.Equal(201, faculty.Code);

            var department = await Client.PostAsync("/api/v1/departments", new { facultyId = faculty.GuidOf("id"), code = $"D{code[1..]}", name = "Department" }, admin);
            Assert.Equal(201, department.Code);

            var program = await Client.PostAsync("/api/v1/academic-programs", new { departmentId = department.GuidOf("id"), code = $"P{code[1..]}", name = "Program", degreeType = "Bachelor", durationYears = 4, totalRequiredCredits = 140 }, admin);
            Assert.Equal(201, program.Code);

            return (universityId, program.GuidOf("id"));
        }

        [Fact]
        public async Task The_real_verifier_accepts_existing_ids_and_refuses_unknown_or_inactive_ones()
        {
            var admin = await fixture.Api.AdminTokenAsync();
            var (university, program) = await CreateProgramAsync(admin);

            await using var scope = fixture.Services.CreateAsyncScope();
            var verifier = new FinanceReferenceVerifier(scope.ServiceProvider.GetRequiredService<AppDbContext>());

            Assert.True((await verifier.VerifyUniversityAsync(university)).IsSuccess);
            Assert.True((await verifier.VerifyProgramAsync(program)).IsSuccess);

            var noUniversity = await verifier.VerifyUniversityAsync(Guid.NewGuid());
            Assert.Equal("UNIVERSITY_NOT_FOUND", noUniversity.Error?.Code);

            var noProgram = await verifier.VerifyProgramAsync(Guid.NewGuid());
            Assert.Equal("PROGRAM_NOT_FOUND", noProgram.Error?.Code);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/academic-programs/{program}/deactivate", null, admin)).Code);

            var inactive = await verifier.VerifyProgramAsync(program);
            Assert.Equal("PROGRAM_INACTIVE", inactive.Error?.Code);

            // The semester is accepted until the academic calendar exists.
            Assert.True((await verifier.VerifySemesterAsync(Guid.NewGuid())).IsSuccess);
        }
    }
}
