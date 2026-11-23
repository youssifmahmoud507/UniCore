using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Finance follows the registration. A per-credit fee becomes one charge per enrollment (credits x price); a seat given up
    // cancels the charge while it is pending, and leaves an invoiced charge for the finance office to review.
    [Collection(ApiCollection.Name)]
    public sealed class EnrollmentChargesApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private OutboxProcessor Processor => fixture.Services.GetRequiredService<OutboxProcessor>();

        private sealed record Charge(Guid Id, decimal Amount, string Status, string Description);

        private async Task<List<Charge>> ChargesAsync(string admin, Guid student, Guid semester)
        {
            var response = await Client.GetAsync($"/api/v1/student-charges?studentId={student}&academicSemesterId={semester}&pageSize=100", admin);
            Assert.Equal(200, response.Code);

            return [.. response.Prop("items").EnumerateArray().Select(c => new Charge(c.GetProperty("id").GetGuid(), c.GetProperty("amount").GetDecimal(), c.GetProperty("status").GetString()!, c.GetProperty("description").GetString()!))];
        }

        private async Task<List<Charge>> EventuallyAsync(string admin, Guid student, Guid semester, Func<List<Charge>, bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            var charges = await ChargesAsync(admin, student, semester);

            while (!condition(charges) && DateTime.UtcNow < deadline)
            {
                await Processor.ProcessBatchAsync(200);
                charges = await ChargesAsync(admin, student, semester);

                if (!condition(charges))
                {
                    await Task.Delay(300);
                }
            }

            Assert.True(condition(charges), "The charges did not reach the expected state.");

            return charges;
        }

        private async Task<Guid> FeeAsync(string admin, string name, decimal amount, string basis)
        {
            var code = $"E{Guid.NewGuid():N}"[..12].ToUpperInvariant();
            var definition = await Client.PostAsync("/api/v1/fee-definitions", new { universityId = Guid.NewGuid(), code, name, description = "x", defaultAmount = amount, basis }, admin);
            Assert.Equal(201, definition.Code);
            Assert.Equal(basis, definition.Str("basis"));

            return definition.GuidOf("id");
        }

        [Fact]
        public async Task Journey_per_credit_fees_follow_every_seat_taken_and_given_up()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2, credits: 3);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var semester = setup.Calendar.SemesterId;

            // Tuition is 1000 per credit hour; the registration fee is a fixed 500 per semester.
            var tuition = await FeeAsync(admin, "Tuition per credit", 1000m, "PerCredit");
            var registration = await FeeAsync(admin, "Registration", 500m, "Fixed");

            var structure = await Client.PostAsync("/api/v1/fee-structures", new { academicProgramId = setup.ProgramId, academicSemesterId = semester, name = "Per credit" }, admin);
            Assert.Equal(201, structure.Code);
            var structureId = structure.GuidOf("id");
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/items", new { feeDefinitionId = tuition, amount = (decimal?)null }, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/items", new { feeDefinitionId = registration, amount = (decimal?)null }, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/activate", null, admin)).Code);

            Task<ApiResponse> RegisterAsync(int course)
                => Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = semester, courseSectionId = setup.Courses[course].SectionId }, admin, TestApi.NewIdempotencyKey());

            // 1. Two courses of 3 credits: two charges of 3000, one per enrollment.
            var first = await RegisterAsync(0);
            Assert.Equal(201, first.Code);
            var second = await RegisterAsync(1);
            Assert.Equal(201, second.Code);

            var charges = await EventuallyAsync(admin, student.Id, semester, c => c.Count(x => x.Status == "Pending") == 2);
            Assert.All(charges, c => Assert.Equal(3000m, c.Amount));

            // 2. The bulk generation adds only the fixed fee and never a second tuition charge.
            var generated = await Client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = semester, academicProgramId = setup.ProgramId, dueDate = AdmitSpec.Today.AddDays(30) }, admin);
            Assert.Equal(200, generated.Code);

            charges = await ChargesAsync(admin, student.Id, semester);
            Assert.Equal(3, charges.Count(c => c.Status == "Pending"));
            Assert.Single(charges, c => c.Amount == 500m);
            Assert.Equal(2, charges.Count(c => c.Amount == 3000m));

            // 3. Dropping the first course cancels its charge only.
            var registrationId = first.Prop("registration").GetProperty("id").GetGuid();
            var dropped = await Client.PostAsync($"/api/v1/registrations/{registrationId}/drop", new { enrollmentIds = new[] { first.Prop("enrollment").GetProperty("id").GetGuid() } }, admin);
            Assert.Equal(200, dropped.Code);

            charges = await EventuallyAsync(admin, student.Id, semester, c => c.Count(x => x.Status == "Cancelled") == 1);
            Assert.Equal(1, charges.Count(c => c.Amount == 3000m && c.Status == "Pending"));
            Assert.Equal(1, charges.Count(c => c.Amount == 500m && c.Status == "Pending"));

            // 4. The semester invoice carries what is left: 3000 + 500.
            var invoice = await Client.PostAsync("/api/v1/invoices", new { studentId = student.Id, academicSemesterId = semester }, admin);
            Assert.Equal(201, invoice.Code);
            Assert.Equal(3500m, invoice.Prop("totalAmount").GetDecimal());

            // 5. Dropping the second course after the invoice does not touch the invoiced charge: it is flagged for review.
            var secondDrop = await Client.PostAsync($"/api/v1/registrations/{registrationId}/drop", new { enrollmentIds = new[] { second.Prop("enrollment").GetProperty("id").GetGuid() } }, admin);
            Assert.Equal(200, secondDrop.Code);

            var invoiced = (await ChargesAsync(admin, student.Id, semester)).Single(c => c.Amount == 3000m && c.Status == "Invoiced");

            var flagged = false;
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!flagged && DateTime.UtcNow < deadline)
            {
                await Processor.ProcessBatchAsync(200);
                var audit = await Client.GetAsync($"/api/v1/audit-logs?action=ENROLLMENT_CHARGE_NEEDS_REVIEW&entityId={invoiced.Id}", admin);
                Assert.Equal(200, audit.Code);
                flagged = audit.Prop("totalCount").GetInt32() >= 1;

                if (!flagged)
                {
                    await Task.Delay(300);
                }
            }

            Assert.True(flagged, "The invoiced charge of the dropped seat was not flagged for review.");
            Assert.Equal(3500m, (await Client.GetAsync($"/api/v1/invoices/{invoice.GuidOf("id")}", admin)).Prop("totalAmount").GetDecimal());
        }
    }
}
