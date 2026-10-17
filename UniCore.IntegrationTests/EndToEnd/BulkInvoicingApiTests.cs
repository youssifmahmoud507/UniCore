using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class BulkInvoicingApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewCode() => $"B{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // Same program, a different person.
        private static object BodyFor(AdmitSpec spec) => new
        {
            personProfileId = Guid.NewGuid(),
            email = (string?)null,
            admissionDate = spec.Admission,
            academicProgramId = spec.Program,
            programCode = spec.Code,
            studyPlanVersionId = spec.Plan,
            admissionAcademicYearId = spec.AcademicYear,
            admissionYear = spec.Admission.Year,
            createAccount = false
        };

        private async Task<Guid> AdmitAsync(string admin, object body)
        {
            var admitted = await Client.PostAsync("/api/v1/students", body, admin);
            Assert.Equal(201, admitted.Code);

            return admitted.Prop("student").GetProperty("summary").GetProperty("id").GetGuid();
        }

        [Fact]
        public async Task Every_student_with_pending_charges_gets_one_invoice()
        {
            var admin = await _api.AdminTokenAsync();
            var spec = new AdmitSpec();
            var semester = Guid.NewGuid();

            var first = await AdmitAsync(admin, spec.Body);
            var second = await AdmitAsync(admin, BodyFor(spec));

            var structure = await Client.PostAsync("/api/v1/fee-structures", new { academicProgramId = spec.Program, academicSemesterId = semester, name = "Bulk" }, admin);
            Assert.Equal(201, structure.Code);
            var structureId = structure.GuidOf("id");

            foreach (var amount in new[] { 700m, 150m })
            {
                var definition = await Client.PostAsync("/api/v1/fee-definitions", new { universityId = Guid.NewGuid(), code = NewCode(), name = "Fee", description = "x", defaultAmount = amount }, admin);
                Assert.Equal(201, definition.Code);
                Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/items", new { feeDefinitionId = definition.GuidOf("id"), amount = (decimal?)null }, admin)).Code);
            }

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/activate", null, admin)).Code);

            var generated = await Client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = semester, academicProgramId = spec.Program, dueDate = AdmitSpec.Today.AddDays(30) }, admin);
            Assert.Equal(200, generated.Code);
            Assert.Equal(4, generated.Prop("chargesCreated").GetInt32());

            var run = await Client.PostAsync("/api/v1/invoices/issue-semester", new { academicSemesterId = semester }, admin);
            Assert.Equal(200, run.Code);
            Assert.Equal(2, run.Prop("studentsInvoiced").GetInt32());
            Assert.Equal(0, run.Prop("studentsFailed").GetInt32());

            foreach (var student in new[] { first, second })
            {
                var invoices = await Client.GetAsync($"/api/v1/invoices?studentId={student}", admin);
                Assert.Equal(1, invoices.Prop("items").GetArrayLength());
                Assert.Equal(850m, invoices.Prop("items")[0].GetProperty("totalAmount").GetDecimal());
            }

            // Nothing is left to invoice, so a second run does nothing.
            var again = await Client.PostAsync("/api/v1/invoices/issue-semester", new { academicSemesterId = semester }, admin);
            Assert.Equal(200, again.Code);
            Assert.Equal(0, again.Prop("studentsInvoiced").GetInt32());
        }

        [Fact]
        public async Task A_bad_request_is_rejected()
        {
            var admin = await _api.AdminTokenAsync();

            var noSemester = await Client.PostAsync("/api/v1/invoices/issue-semester", new { }, admin);
            Assert.Equal(400, noSemester.Code);
            Assert.Equal("INVOICE_DATA_INVALID", noSemester.ErrorCode);

            var past = await Client.PostAsync("/api/v1/invoices/issue-semester", new { academicSemesterId = Guid.NewGuid(), dueDate = AdmitSpec.Today.AddDays(-1) }, admin);
            Assert.Equal(400, past.Code);
        }

        [Fact]
        public async Task Only_finance_staff_can_run_it()
        {
            var plain = await _api.CreateUserAsync("plain");
            var token = await _api.LoginTokenAsync(plain.UserName, plain.Password);

            Assert.Equal(403, (await Client.PostAsync("/api/v1/invoices/issue-semester", new { academicSemesterId = Guid.NewGuid() }, token)).Code);
            Assert.Equal(401, (await Client.PostAsync("/api/v1/invoices/issue-semester", new { academicSemesterId = Guid.NewGuid() })).Code);
        }
    }
}
