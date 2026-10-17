using System.Text.Json;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class ServiceChargesApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewCode() => $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        private static string NewReference() => $"REQ-{Guid.NewGuid():N}";

        private static DateOnly Due => AdmitSpec.Today.AddDays(10);

        private async Task<Guid> AdmitAsync(string admin)
        {
            var admitted = await Client.PostAsync("/api/v1/students", new AdmitSpec().Body, admin);
            Assert.Equal(201, admitted.Code);

            return admitted.Prop("student").GetProperty("summary").GetProperty("id").GetGuid();
        }

        private async Task<string> FeeAsync(string admin, decimal amount, string? code = null, Guid? universityId = null)
        {
            var feeCode = code ?? NewCode();

            var created = await Client.PostAsync("/api/v1/fee-definitions", new { universityId = universityId ?? Guid.NewGuid(), code = feeCode, name = "Transcript fee", description = "x", defaultAmount = amount }, admin);
            Assert.Equal(201, created.Code);

            return feeCode;
        }

        private Task<ApiResponse> ChargeAsync(string admin, Guid student, string code, string reference, bool issueInvoice = false, decimal? amount = null)
            => Client.PostAsync("/api/v1/student-charges/service", new { studentId = student, feeCode = code, reference, dueDate = Due, amount, issueInvoice }, admin);

        [Fact]
        public async Task A_service_charge_is_created_once_per_reference()
        {
            var admin = await _api.AdminTokenAsync();
            var student = await AdmitAsync(admin);
            var code = await FeeAsync(admin, 150m);
            var reference = NewReference();

            var first = await ChargeAsync(admin, student, code, reference);
            Assert.Equal(201, first.Code);
            Assert.True(first.Prop("created").GetBoolean());
            Assert.Equal(150m, first.Prop("charge").GetProperty("amount").GetDecimal());
            Assert.Equal("Pending", first.Prop("charge").GetProperty("status").GetString());
            Assert.Equal(reference, first.Prop("charge").GetProperty("serviceReference").GetString());
            var chargeId = first.Prop("charge").GetProperty("id").GetGuid();

            var again = await ChargeAsync(admin, student, code, reference);
            Assert.Equal(200, again.Code);
            Assert.False(again.Prop("created").GetBoolean());
            Assert.Equal(chargeId, again.Prop("charge").GetProperty("id").GetGuid());

            var list = await Client.GetAsync($"/api/v1/student-charges?studentId={student}", admin);
            Assert.Equal(1, list.Prop("items").GetArrayLength());
        }

        [Fact]
        public async Task A_custom_amount_replaces_the_default()
        {
            var admin = await _api.AdminTokenAsync();
            var student = await AdmitAsync(admin);
            var code = await FeeAsync(admin, 150m);

            var result = await ChargeAsync(admin, student, code, NewReference(), amount: 99.5m);

            Assert.Equal(201, result.Code);
            Assert.Equal(99.5m, result.Prop("charge").GetProperty("amount").GetDecimal());
        }

        [Fact]
        public async Task The_invoice_covers_only_the_service_charge()
        {
            var admin = await _api.AdminTokenAsync();
            var student = await AdmitAsync(admin);
            var code = await FeeAsync(admin, 200m);
            var reference = NewReference();

            var result = await ChargeAsync(admin, student, code, reference, issueInvoice: true);

            Assert.Equal(201, result.Code);
            Assert.Equal(200m, result.Prop("invoice").GetProperty("totalAmount").GetDecimal());
            Assert.Equal(1, result.Prop("invoice").GetProperty("lines").GetArrayLength());

            var chargeId = result.Prop("charge").GetProperty("id").GetGuid();
            var charge = await Client.GetAsync($"/api/v1/student-charges/{chargeId}", admin);
            Assert.Equal("Invoiced", charge.Str("status"));

            // A retry returns the same charge and does not issue a second invoice.
            var again = await ChargeAsync(admin, student, code, reference, issueInvoice: true);
            Assert.Equal(200, again.Code);
            Assert.Equal(JsonValueKind.Null, again.Prop("invoice").ValueKind);

            var invoices = await Client.GetAsync($"/api/v1/invoices?studentId={student}", admin);
            Assert.Equal(1, invoices.Prop("items").GetArrayLength());
        }

        [Fact]
        public async Task Bad_requests_are_rejected_with_clear_codes()
        {
            var admin = await _api.AdminTokenAsync();
            var student = await AdmitAsync(admin);
            var code = await FeeAsync(admin, 50m);

            var unknown = await ChargeAsync(admin, student, NewCode(), NewReference());
            Assert.Equal(404, unknown.Code);
            Assert.Equal("FEE_DEFINITION_NOT_FOUND", unknown.ErrorCode);

            var blank = await ChargeAsync(admin, student, code, "   ");
            Assert.Equal(400, blank.Code);
            Assert.Equal("SERVICE_REFERENCE_INVALID", blank.ErrorCode);

            var badCode = await ChargeAsync(admin, student, "a b", NewReference());
            Assert.Equal(400, badCode.Code);
            Assert.Equal("FEE_CODE_INVALID", badCode.ErrorCode);

            var missingStudent = await ChargeAsync(admin, Guid.NewGuid(), code, NewReference());
            Assert.Equal(404, missingStudent.Code);

            var past = await Client.PostAsync("/api/v1/student-charges/service", new { studentId = student, feeCode = code, reference = NewReference(), dueDate = AdmitSpec.Today.AddDays(-1) }, admin);
            Assert.Equal(400, past.Code);
            Assert.Equal("CHARGE_DUE_DATE_INVALID", past.ErrorCode);
        }

        [Fact]
        public async Task The_same_code_in_two_universities_is_ambiguous()
        {
            var admin = await _api.AdminTokenAsync();
            var student = await AdmitAsync(admin);
            var code = NewCode();
            await FeeAsync(admin, 10m, code, Guid.NewGuid());
            await FeeAsync(admin, 20m, code, Guid.NewGuid());

            var result = await ChargeAsync(admin, student, code, NewReference());

            Assert.Equal(409, result.Code);
            Assert.Equal("FEE_CODE_AMBIGUOUS", result.ErrorCode);
        }

        [Fact]
        public async Task Only_finance_staff_can_create_service_charges()
        {
            var plain = await _api.CreateUserAsync("plain");
            var token = await _api.LoginTokenAsync(plain.UserName, plain.Password);

            var denied = await ChargeAsync(token, Guid.NewGuid(), NewCode(), NewReference());
            Assert.Equal(403, denied.Code);

            var anonymous = await Client.PostAsync("/api/v1/student-charges/service", new { studentId = Guid.NewGuid() });
            Assert.Equal(401, anonymous.Code);
        }
    }

}
