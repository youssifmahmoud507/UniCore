using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class InvoicingApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewCode() => $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        private static DateOnly Due => AdmitSpec.Today.AddDays(30);

        private async Task<(AdmitSpec Spec, Guid Id)> AdmitAsync(string admin)
        {
            var spec = new AdmitSpec();

            var created = await Client.PostAsync("/api/v1/students", spec.Body, admin);
            Assert.Equal(201, created.Code);

            var id = created.Prop("student").GetProperty("summary").GetProperty("id").GetGuid();

            return (spec, id);
        }

        private async Task<Guid> ActiveStructureAsync(string admin, Guid? program, Guid? semester, params decimal[] amounts)
        {
            var created = await Client.PostAsync("/api/v1/fee-structures", new { academicProgramId = program, academicSemesterId = semester, name = "Fall" }, admin);
            Assert.Equal(201, created.Code);
            var id = created.GuidOf("id");

            foreach (var amount in amounts)
            {
                var definition = await Client.PostAsync("/api/v1/fee-definitions", new { universityId = Guid.NewGuid(), code = NewCode(), name = "Fee", description = "x", defaultAmount = amount }, admin);
                Assert.Equal(201, definition.Code);

                var added = await Client.PostAsync($"/api/v1/fee-structures/{id}/items", new { feeDefinitionId = definition.GuidOf("id"), amount = (decimal?)null }, admin);
                Assert.Equal(200, added.Code);
            }

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{id}/activate", null, admin)).Code);

            return id;
        }

        private Task<ApiResponse> GenerateAsync(string admin, Guid semester, Guid? program)
            => Client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = semester, academicProgramId = program, dueDate = Due }, admin);

        [Fact]
        public async Task Generation_creates_the_charges_and_running_it_again_creates_nothing()
        {
            var admin = await _api.AdminTokenAsync();
            var semester = Guid.NewGuid();
            var (spec, studentId) = await AdmitAsync(admin);
            await ActiveStructureAsync(admin, spec.Program, semester, 1000m, 250.50m);

            var first = await GenerateAsync(admin, semester, spec.Program);
            Assert.Equal(200, first.Code);
            Assert.Equal(1, first.Prop("studentsProcessed").GetInt32());
            Assert.Equal(2, first.Prop("chargesCreated").GetInt32());

            var second = await GenerateAsync(admin, semester, spec.Program);
            Assert.Equal(200, second.Code);
            Assert.Equal(0, second.Prop("chargesCreated").GetInt32());
            Assert.Equal(2, second.Prop("chargesAlreadyExisting").GetInt32());

            var list = await Client.GetAsync($"/api/v1/student-charges?studentId={studentId}&academicSemesterId={semester}", admin);
            Assert.Equal(200, list.Code);
            Assert.Equal(2, list.Prop("items").GetArrayLength());
        }

        [Fact]
        public async Task The_most_specific_structure_wins()
        {
            var admin = await _api.AdminTokenAsync();
            var semester = Guid.NewGuid();
            var (specialist, specialistId) = await AdmitAsync(admin);
            var (ordinary, ordinaryId) = await AdmitAsync(admin);

            await ActiveStructureAsync(admin, specialist.Program, semester, 1000m);
            await ActiveStructureAsync(admin, null, semester, 500m);

            Assert.Equal(200, (await GenerateAsync(admin, semester, specialist.Program)).Code);
            Assert.Equal(200, (await GenerateAsync(admin, semester, ordinary.Program)).Code);

            var specialistCharges = await Client.GetAsync($"/api/v1/student-charges?studentId={specialistId}", admin);
            var ordinaryCharges = await Client.GetAsync($"/api/v1/student-charges?studentId={ordinaryId}", admin);

            Assert.Equal(1000m, specialistCharges.Prop("items")[0].GetProperty("amount").GetDecimal());
            Assert.Equal(500m, ordinaryCharges.Prop("items")[0].GetProperty("amount").GetDecimal());
        }

        [Fact]
        public async Task A_student_without_a_matching_structure_is_counted_and_gets_no_charge()
        {
            var admin = await _api.AdminTokenAsync();
            var (spec, studentId) = await AdmitAsync(admin);

            var result = await GenerateAsync(admin, Guid.NewGuid(), spec.Program);

            Assert.Equal(200, result.Code);
            Assert.Equal(1, result.Prop("studentsWithoutStructure").GetInt32());
            Assert.Equal(0, result.Prop("chargesCreated").GetInt32());
        }

        [Fact]
        public async Task A_due_date_in_the_past_is_rejected()
        {
            var admin = await _api.AdminTokenAsync();

            var result = await Client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = Guid.NewGuid(), dueDate = AdmitSpec.Today.AddDays(-1) }, admin);

            Assert.Equal(400, result.Code);
            Assert.Equal("CHARGE_DUE_DATE_INVALID", result.ErrorCode);
        }

        [Fact]
        public async Task A_cancelled_charge_is_generated_again()
        {
            var admin = await _api.AdminTokenAsync();
            var semester = Guid.NewGuid();
            var (spec, studentId) = await AdmitAsync(admin);
            await ActiveStructureAsync(admin, spec.Program, semester, 700m);

            Assert.Equal(1, (await GenerateAsync(admin, semester, spec.Program)).Prop("chargesCreated").GetInt32());

            var charges = await Client.GetAsync($"/api/v1/student-charges?studentId={studentId}", admin);
            var chargeId = charges.Prop("items")[0].GetProperty("id").GetGuid();

            var cancelled = await Client.PostAsync($"/api/v1/student-charges/{chargeId}/cancel", null, admin);
            Assert.Equal(200, cancelled.Code);
            Assert.Equal("Cancelled", cancelled.Str("status"));

            var again = await Client.PostAsync($"/api/v1/student-charges/{chargeId}/cancel", null, admin);
            Assert.Equal(422, again.Code);
            Assert.Equal("STUDENT_CHARGE_NOT_PENDING", again.ErrorCode);

            Assert.Equal(1, (await GenerateAsync(admin, semester, spec.Program)).Prop("chargesCreated").GetInt32());
        }

        [Fact]
        public async Task An_invoice_collects_the_pending_charges_and_cannot_be_issued_twice()
        {
            var admin = await _api.AdminTokenAsync();
            var semester = Guid.NewGuid();
            var (spec, studentId) = await AdmitAsync(admin);
            await ActiveStructureAsync(admin, spec.Program, semester, 1000m, 250.50m);
            Assert.Equal(200, (await GenerateAsync(admin, semester, spec.Program)).Code);

            var invoice = await Client.PostAsync("/api/v1/invoices", new { studentId }, admin);

            Assert.Equal(201, invoice.Code);
            Assert.StartsWith("INV-", invoice.Str("invoiceNumber"));
            Assert.Equal(1250.50m, invoice.Prop("totalAmount").GetDecimal());
            Assert.Equal(1250.50m, invoice.Prop("balance").GetDecimal());
            Assert.Equal(0m, invoice.Prop("paidAmount").GetDecimal());
            Assert.Equal("Issued", invoice.Str("status"));
            Assert.Equal(2, invoice.Prop("lines").GetArrayLength());

            var pending = await Client.GetAsync($"/api/v1/student-charges?studentId={studentId}&status=Pending", admin);
            Assert.Equal(0, pending.Prop("items").GetArrayLength());

            var again = await Client.PostAsync("/api/v1/invoices", new { studentId }, admin);
            Assert.Equal(422, again.Code);
            Assert.Equal("NO_PENDING_CHARGES", again.ErrorCode);

            var fetched = await Client.GetAsync($"/api/v1/invoices/{invoice.GuidOf("id")}", admin);
            Assert.Equal(200, fetched.Code);

            var list = await Client.GetAsync($"/api/v1/invoices?studentId={studentId}", admin);
            Assert.Equal(1, list.Prop("items").GetArrayLength());
        }

        [Fact]
        public async Task Two_parallel_invoice_requests_produce_exactly_one_invoice()
        {
            var admin = await _api.AdminTokenAsync();
            var semester = Guid.NewGuid();
            var (spec, studentId) = await AdmitAsync(admin);
            await ActiveStructureAsync(admin, spec.Program, semester, 400m);
            Assert.Equal(200, (await GenerateAsync(admin, semester, spec.Program)).Code);

            var results = await Task.WhenAll(
                Client.PostAsync("/api/v1/invoices", new { studentId }, admin),
                Client.PostAsync("/api/v1/invoices", new { studentId }, admin));

            Assert.Single(results, r => r.Code == 201);
            Assert.Single(results, r => r.Code == 409 || r.Code == 422);

            var list = await Client.GetAsync($"/api/v1/invoices?studentId={studentId}", admin);
            Assert.Equal(1, list.Prop("items").GetArrayLength());
        }

        [Fact]
        public async Task Invoice_numbers_are_unique_and_increasing()
        {
            var admin = await _api.AdminTokenAsync();
            var numbers = new List<string>();

            for (var i = 0; i < 3; i++)
            {
                var semester = Guid.NewGuid();
                var (spec, studentId) = await AdmitAsync(admin);
                await ActiveStructureAsync(admin, spec.Program, semester, 100m);
                Assert.Equal(200, (await GenerateAsync(admin, semester, spec.Program)).Code);

                var invoice = await Client.PostAsync("/api/v1/invoices", new { studentId }, admin);
                Assert.Equal(201, invoice.Code);
                numbers.Add(invoice.Str("invoiceNumber"));
            }

            Assert.Equal(3, numbers.Distinct().Count());
            Assert.Equal(numbers.OrderBy(n => n, StringComparer.Ordinal).ToList(), numbers);
        }

        [Fact]
        public async Task Anonymous_and_unprivileged_callers_are_rejected()
        {
            Assert.Equal(401, (await Client.PostAsync("/api/v1/invoices", new { studentId = Guid.NewGuid() })).Code);

            var plain = await _api.CreateUserAsync("plain");
            var token = await _api.LoginTokenAsync(plain.Email, plain.Password);

            Assert.Equal(403, (await Client.PostAsync("/api/v1/invoices", new { studentId = Guid.NewGuid() }, token)).Code);
            Assert.Equal(403, (await Client.GetAsync("/api/v1/student-charges", token)).Code);
        }
    }



}
