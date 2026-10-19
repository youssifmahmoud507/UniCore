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

            var noSemester = await verifier.VerifySemesterAsync(Guid.NewGuid());
            Assert.Equal("SEMESTER_NOT_FOUND", noSemester.Error?.Code);

            var semester = await CreateSemesterAsync(admin);
            Assert.True((await verifier.VerifySemesterAsync(semester)).IsSuccess);
        }

        [Fact]
        public async Task A_closed_semester_is_refused()
        {
            var admin = await fixture.Api.AdminTokenAsync();
            var semester = await CreateSemesterAsync(admin, closed: true);

            await using var scope = fixture.Services.CreateAsyncScope();
            var verifier = new FinanceReferenceVerifier(scope.ServiceProvider.GetRequiredService<AppDbContext>());

            var result = await verifier.VerifySemesterAsync(semester);

            Assert.Equal("SEMESTER_CLOSED", result.Error?.Code);
        }

        // Every call takes a new far-future year, so years never overlap.
        private static int _calendarCounter = 2300;

        private async Task<Guid> CreateSemesterAsync(string admin, bool closed = false)
        {
            var n = Interlocked.Increment(ref _calendarCounter);
            var start = new DateOnly(n, 9, 1);
            var end = new DateOnly(n + 1, 6, 30);

            var year = await Client.PostAsync("/api/v1/academic-years", new { name = $"{n}/{n + 1}", startDate = start, endDate = end }, admin);
            Assert.Equal(201, year.Code);

            var term = await Client.PostAsync("/api/v1/academic-terms", new { code = $"T{n}", name = $"Term {n}", sortOrder = 1 }, admin);
            Assert.Equal(201, term.Code);

            var windows = new { registrationStart = start, registrationEnd = start.AddDays(10), addDropStart = start, addDropEnd = start.AddDays(20) };

            var semester = await Client.PostAsync("/api/v1/semesters", new { academicYearId = year.GuidOf("id"), academicTermId = term.GuidOf("id"), startDate = start, endDate = end, windows }, admin);
            Assert.Equal(201, semester.Code);
            var id = semester.GuidOf("id");

            if (closed)
            {
                foreach (var status in new[] { "RegistrationOpen", "InProgress", "Examinations", "GradingOpen", "Closed" })
                {
                    Assert.Equal(200, (await Client.PostAsync($"/api/v1/semesters/{id}/advance", new { status }, admin)).Code);
                }
            }

            return id;
        }
    }
}
