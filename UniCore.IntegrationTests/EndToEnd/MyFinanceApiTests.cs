using System.Text.Json;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class MyFinanceApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewCode() => $"M{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // A student with a login account, one fee of the given amount, and the invoice for it.
        private async Task<(Guid StudentId, string Token, Guid InvoiceId)> StudentWithInvoiceAsync(string admin, decimal amount)
        {
            var email = $"{TestApi.NewName("mine")}@uni.test";
            var spec = new AdmitSpec(null, email, createAccount: true);

            var admitted = await Client.PostAsync("/api/v1/students", spec.Body, admin);
            Assert.Equal(201, admitted.Code);
            var studentId = admitted.Prop("student").GetProperty("summary").GetProperty("id").GetGuid();

            await _api.SetPasswordAsync(email, TestApi.DefaultPassword);
            var token = await _api.LoginTokenAsync(spec.Number(1), TestApi.DefaultPassword);

            var semester = Guid.NewGuid();

            var structure = await Client.PostAsync("/api/v1/fee-structures", new { academicProgramId = spec.Program, academicSemesterId = semester, name = "Mine" }, admin);
            Assert.Equal(201, structure.Code);
            var structureId = structure.GuidOf("id");

            var definition = await Client.PostAsync("/api/v1/fee-definitions", new { universityId = Guid.NewGuid(), code = NewCode(), name = "Fee", description = "x", defaultAmount = amount }, admin);
            Assert.Equal(201, definition.Code);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/items", new { feeDefinitionId = definition.GuidOf("id"), amount = (decimal?)null }, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/activate", null, admin)).Code);

            var generated = await Client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = semester, academicProgramId = spec.Program, dueDate = AdmitSpec.Today.AddDays(30) }, admin);
            Assert.Equal(200, generated.Code);

            var invoice = await Client.PostAsync("/api/v1/invoices", new { studentId, academicSemesterId = semester }, admin);
            Assert.Equal(201, invoice.Code);

            return (studentId, token, invoice.GuidOf("id"));
        }

        [Fact]
        public async Task A_student_sees_their_own_balance_invoices_and_payments()
        {
            var admin = await _api.AdminTokenAsync();
            var mine = await StudentWithInvoiceAsync(admin, 1000m);

            var finance = await Client.GetAsync("/api/v1/me/finance", mine.Token);
            Assert.Equal(200, finance.Code);
            Assert.Equal(1000m, finance.Prop("balance").GetProperty("outstanding").GetDecimal());
            Assert.Equal(0, finance.Prop("activeHolds").GetInt32());
            Assert.False(finance.Prop("blocksRegistration").GetBoolean());

            var list = await Client.GetAsync("/api/v1/me/invoices", mine.Token);
            Assert.Equal(200, list.Code);
            Assert.Equal(1, list.Prop("items").GetArrayLength());

            var detail = await Client.GetAsync($"/api/v1/me/invoices/{mine.InvoiceId}", mine.Token);
            Assert.Equal(200, detail.Code);
            Assert.Equal(1000m, detail.Prop("totalAmount").GetDecimal());

            var paid = await Client.PostAsync(
                $"/api/v1/invoices/{mine.InvoiceId}/payments",
                new { amount = 400m, method = "Cash" },
                admin,
                new Dictionary<string, string> { ["Idempotency-Key"] = $"mine-{Guid.NewGuid():N}" });
            Assert.Equal(201, paid.Code);

            var payments = await Client.GetAsync("/api/v1/me/payments", mine.Token);
            Assert.Equal(200, payments.Code);
            Assert.Equal(1, payments.Prop("items").GetArrayLength());
            var payment = payments.Prop("items")[0];
            Assert.Equal(400m, payment.GetProperty("amount").GetDecimal());
            Assert.False(payment.TryGetProperty("recordedByUserId", out _));

            // The staff account that recorded the payment is hidden inside the invoice too.
            var after = await Client.GetAsync($"/api/v1/me/invoices/{mine.InvoiceId}", mine.Token);
            Assert.Equal(JsonValueKind.Null, after.Prop("payments")[0].GetProperty("recordedByUserId").ValueKind);

            var balance = await Client.GetAsync("/api/v1/me/finance", mine.Token);
            Assert.Equal(600m, balance.Prop("balance").GetProperty("outstanding").GetDecimal());
        }

        [Fact]
        public async Task A_student_cannot_read_another_students_invoice()
        {
            var admin = await _api.AdminTokenAsync();
            var mine = await StudentWithInvoiceAsync(admin, 300m);
            var other = await InvoiceScenario.IssueAsync(_api, admin, 500m);

            var denied = await Client.GetAsync($"/api/v1/me/invoices/{other.InvoiceId}", mine.Token);
            Assert.Equal(404, denied.Code);
            Assert.Equal("INVOICE_NOT_FOUND", denied.ErrorCode);

            var list = await Client.GetAsync("/api/v1/me/invoices", mine.Token);
            Assert.Equal(1, list.Prop("items").GetArrayLength());
        }

        [Fact]
        public async Task The_staff_endpoints_stay_closed_to_the_student()
        {
            var admin = await _api.AdminTokenAsync();
            var mine = await StudentWithInvoiceAsync(admin, 200m);

            Assert.Equal(403, (await Client.GetAsync("/api/v1/invoices", mine.Token)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/invoices/{mine.InvoiceId}", mine.Token)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/students/{mine.StudentId}/finance", mine.Token)).Code);
            Assert.Equal(403, (await Client.PostAsync("/api/v1/invoices", new { studentId = mine.StudentId }, mine.Token)).Code);
        }

        [Fact]
        public async Task A_user_without_a_student_record_and_an_anonymous_caller_are_turned_away()
        {
            var admin = await _api.AdminTokenAsync();

            var noStudent = await Client.GetAsync("/api/v1/me/finance", admin);
            Assert.Equal(404, noStudent.Code);

            var anonymous = await Client.GetAsync("/api/v1/me/finance");
            Assert.Equal(401, anonymous.Code);
        }
    }
}
