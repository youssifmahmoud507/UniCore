using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Outbox;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class PaymentsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewKey() => $"key-{Guid.NewGuid():N}";

        private Task<ApiResponse> PayAsync(string token, Guid invoice, decimal? amount, string? key, string? method = "Cash", string? reference = null, DateOnly? date = null)
        {
            var headers = key is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["Idempotency-Key"] = key };

            return Client.PostAsync($"/api/v1/invoices/{invoice}/payments", new { amount, method, reference, paymentDate = date }, token, headers);
        }

        private Task<ApiResponse> GetInvoiceAsync(string token, Guid invoice) => Client.GetAsync($"/api/v1/invoices/{invoice}", token);

        // ---------- recording payments ----------

        [Fact]
        public async Task Partial_and_full_payments_move_the_invoice_through_its_statuses()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);

            var first = await PayAsync(admin, invoice.InvoiceId, 400m, NewKey());
            Assert.Equal(201, first.Code);
            Assert.Equal(400m, first.Prop("amount").GetDecimal());
            Assert.Equal("Cash", first.Str("method"));

            var afterFirst = await GetInvoiceAsync(admin, invoice.InvoiceId);
            Assert.Equal("PartiallyPaid", afterFirst.Str("status"));
            Assert.Equal(400m, afterFirst.Prop("paidAmount").GetDecimal());
            Assert.Equal(600m, afterFirst.Prop("balance").GetDecimal());

            var second = await PayAsync(admin, invoice.InvoiceId, 600m, NewKey());
            Assert.Equal(201, second.Code);

            var afterSecond = await GetInvoiceAsync(admin, invoice.InvoiceId);
            Assert.Equal("Paid", afterSecond.Str("status"));
            Assert.Equal(0m, afterSecond.Prop("balance").GetDecimal());
            Assert.Equal(2, afterSecond.Prop("payments").GetArrayLength());

            var list = await Client.GetAsync($"/api/v1/invoices/{invoice.InvoiceId}/payments", admin);
            Assert.Equal(200, list.Code);
            Assert.Equal(2, list.Body.GetArrayLength());
        }

        [Fact]
        public async Task Overpaying_or_paying_a_paid_invoice_is_rejected()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 500m);

            var over = await PayAsync(admin, invoice.InvoiceId, 500.01m, NewKey());
            Assert.Equal(422, over.Code);
            Assert.Equal("PAYMENT_EXCEEDS_BALANCE", over.ErrorCode);

            Assert.Equal(201, (await PayAsync(admin, invoice.InvoiceId, 500m, NewKey())).Code);

            var again = await PayAsync(admin, invoice.InvoiceId, 1m, NewKey());
            Assert.Equal(422, again.Code);
            Assert.Equal("INVOICE_ALREADY_PAID", again.ErrorCode);
        }

        [Fact]
        public async Task Bad_input_is_a_validation_error()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 500m);
            var id = invoice.InvoiceId;

            Assert.Equal("IDEMPOTENCY_KEY_INVALID", (await PayAsync(admin, id, 10m, null)).ErrorCode);
            Assert.Equal("IDEMPOTENCY_KEY_INVALID", (await PayAsync(admin, id, 10m, "abc")).ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_REQUIRED", (await PayAsync(admin, id, null, NewKey())).ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_PRECISION_INVALID", (await PayAsync(admin, id, 10.123m, NewKey())).ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", (await PayAsync(admin, id, 0m, NewKey())).ErrorCode);
            Assert.Equal("PAYMENT_DATA_INVALID", (await PayAsync(admin, id, 10m, NewKey(), method: null)).ErrorCode);
            Assert.Equal("PAYMENT_REFERENCE_REQUIRED", (await PayAsync(admin, id, 10m, NewKey(), method: "BankTransfer")).ErrorCode);
            Assert.Equal("PAYMENT_DATE_INVALID", (await PayAsync(admin, id, 10m, NewKey(), date: AdmitSpec.Today.AddDays(1))).ErrorCode);

            var unchanged = await GetInvoiceAsync(admin, id);
            Assert.Equal("Issued", unchanged.Str("status"));
            Assert.Equal(0, unchanged.Prop("payments").GetArrayLength());
        }

        [Fact]
        public async Task A_bank_transfer_with_a_reference_is_accepted()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 500m);

            var paid = await PayAsync(admin, invoice.InvoiceId, 100m, NewKey(), method: "BankTransfer", reference: "TRX-1001");

            Assert.Equal(201, paid.Code);
            Assert.Equal("BankTransfer", paid.Str("method"));
            Assert.Equal("TRX-1001", paid.Str("reference"));
        }

        [Fact]
        public async Task An_unknown_invoice_is_404_and_a_user_without_permission_gets_403()
        {
            var admin = await _api.AdminTokenAsync();
            var user = await _api.CreateUserAsync("paynoperm");
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 500m);

            var missing = await PayAsync(admin, Guid.NewGuid(), 10m, NewKey());
            Assert.Equal(404, missing.Code);
            Assert.Equal("INVOICE_NOT_FOUND", missing.ErrorCode);

            Assert.Equal(403, (await PayAsync(token, invoice.InvoiceId, 10m, NewKey())).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/invoices/{invoice.InvoiceId}/payments", token)).Code);
        }

        // ---------- idempotency ----------

        [Fact]
        public async Task The_same_key_and_the_same_request_returns_the_original_payment_once()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);
            var key = NewKey();

            var first = await PayAsync(admin, invoice.InvoiceId, 400m, key);
            var replay = await PayAsync(admin, invoice.InvoiceId, 400m, key);

            Assert.Equal(201, first.Code);
            Assert.Equal(200, replay.Code);
            Assert.Equal(first.Str("id"), replay.Str("id"));

            var current = await GetInvoiceAsync(admin, invoice.InvoiceId);
            Assert.Equal(400m, current.Prop("paidAmount").GetDecimal());
            Assert.Equal(1, current.Prop("payments").GetArrayLength());
        }

        [Fact]
        public async Task The_same_key_with_a_different_request_is_a_conflict()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);
            var other = await InvoiceScenario.IssueAsync(_api, admin, 1000m);
            var key = NewKey();

            Assert.Equal(201, (await PayAsync(admin, invoice.InvoiceId, 400m, key)).Code);

            var differentAmount = await PayAsync(admin, invoice.InvoiceId, 300m, key);
            var differentInvoice = await PayAsync(admin, other.InvoiceId, 400m, key);

            Assert.Equal(409, differentAmount.Code);
            Assert.Equal("IDEMPOTENCY_KEY_REUSED", differentAmount.ErrorCode);
            Assert.Equal("IDEMPOTENCY_KEY_REUSED", differentInvoice.ErrorCode);
            Assert.Equal(400m, (await GetInvoiceAsync(admin, invoice.InvoiceId)).Prop("paidAmount").GetDecimal());
        }

        [Fact]
        public async Task A_replay_still_works_after_the_invoice_is_fully_paid()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 300m);
            var key = NewKey();

            Assert.Equal(201, (await PayAsync(admin, invoice.InvoiceId, 300m, key)).Code);

            var replay = await PayAsync(admin, invoice.InvoiceId, 300m, key);

            Assert.Equal(200, replay.Code);
        }

        // ---------- concurrency ----------

        [Fact]
        public async Task Two_parallel_payments_cannot_both_take_the_last_of_the_balance()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);

            var results = await Task.WhenAll(
                PayAsync(admin, invoice.InvoiceId, 600m, NewKey()),
                PayAsync(admin, invoice.InvoiceId, 600m, NewKey()));

            Assert.Equal(1, results.Count(r => r.Code == 201));
            Assert.All(results.Where(r => r.Code != 201), r => Assert.Contains(r.ErrorCode ?? string.Empty, new[] { "CONCURRENCY_CONFLICT", "PAYMENT_EXCEEDS_BALANCE" }));

            var current = await GetInvoiceAsync(admin, invoice.InvoiceId);
            Assert.Equal(600m, current.Prop("paidAmount").GetDecimal());
            Assert.Equal(400m, current.Prop("balance").GetDecimal());
        }

        [Fact]
        public async Task Two_parallel_identical_requests_record_one_payment()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);
            var key = NewKey();

            var results = await Task.WhenAll(
                PayAsync(admin, invoice.InvoiceId, 400m, key),
                PayAsync(admin, invoice.InvoiceId, 400m, key));

            Assert.Contains(results, r => r.Code == 201);
            Assert.All(results, r => Assert.Contains(r.Code, new[] { 200, 201, 409 }));

            var current = await GetInvoiceAsync(admin, invoice.InvoiceId);
            Assert.Equal(400m, current.Prop("paidAmount").GetDecimal());
            Assert.Equal(1, current.Prop("payments").GetArrayLength());
        }

        // ---------- cancellation ----------

        [Fact]
        public async Task A_cancelled_invoice_cancels_its_charges_and_they_can_be_generated_again()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 700m);

            var cancelled = await Client.PostAsync($"/api/v1/invoices/{invoice.InvoiceId}/cancel", new { reason = "Issued by mistake" }, admin);
            Assert.Equal(200, cancelled.Code);
            Assert.Equal("Cancelled", cancelled.Str("status"));
            Assert.Equal(0m, cancelled.Prop("balance").GetDecimal());
            Assert.Equal(700m, cancelled.Prop("totalAmount").GetDecimal());
            Assert.Equal(1, cancelled.Prop("lines").GetArrayLength());

            var charges = await Client.GetAsync($"/api/v1/student-charges?studentId={invoice.StudentId}", admin);
            Assert.Equal("Cancelled", charges.Prop("items")[0].GetProperty("status").GetString());

            var pay = await PayAsync(admin, invoice.InvoiceId, 10m, NewKey());
            Assert.Equal(422, pay.Code);
            Assert.Equal("INVOICE_CANCELLED", pay.ErrorCode);

            var again = await Client.PostAsync($"/api/v1/invoices/{invoice.InvoiceId}/cancel", null, admin);
            Assert.Equal(422, again.Code);
            Assert.Equal("INVOICE_CANCELLED", again.ErrorCode);

            var regenerated = await Client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = invoice.Semester, academicProgramId = invoice.Spec.Program, dueDate = AdmitSpec.Today.AddDays(30) }, admin);
            Assert.Equal(1, regenerated.Prop("chargesCreated").GetInt32());

            var reissued = await Client.PostAsync("/api/v1/invoices", new { studentId = invoice.StudentId, academicSemesterId = invoice.Semester }, admin);
            Assert.Equal(201, reissued.Code);
            Assert.NotEqual(invoice.InvoiceId.ToString(), reissued.Str("id"));
        }

        [Fact]
        public async Task An_invoice_with_a_payment_cannot_be_cancelled()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 700m);
            Assert.Equal(201, (await PayAsync(admin, invoice.InvoiceId, 100m, NewKey())).Code);

            var cancel = await Client.PostAsync($"/api/v1/invoices/{invoice.InvoiceId}/cancel", null, admin);

            Assert.Equal(422, cancel.Code);
            Assert.Equal("INVOICE_HAS_PAYMENTS", cancel.ErrorCode);
            Assert.Equal("PartiallyPaid", (await GetInvoiceAsync(admin, invoice.InvoiceId)).Str("status"));
        }

        [Fact]
        public async Task A_cancel_reason_that_is_too_long_is_rejected_and_an_unknown_invoice_is_404()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 700m);

            var tooLong = await Client.PostAsync($"/api/v1/invoices/{invoice.InvoiceId}/cancel", new { reason = new string('r', 201) }, admin);
            var missing = await Client.PostAsync($"/api/v1/invoices/{Guid.NewGuid()}/cancel", null, admin);

            Assert.Equal(400, tooLong.Code);
            Assert.Equal("INVOICE_DATA_INVALID", tooLong.ErrorCode);
            Assert.Equal(404, missing.Code);
            Assert.Equal("Issued", (await GetInvoiceAsync(admin, invoice.InvoiceId)).Str("status"));
        }

        // ---------- audit and events ----------

        [Fact]
        public async Task A_payment_is_audited_and_its_event_is_delivered_by_the_outbox()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);

            var paid = await PayAsync(admin, invoice.InvoiceId, 250m, NewKey());
            Assert.Equal(201, paid.Code);
            var paymentId = paid.Str("id");

            var audit = await Client.GetAsync($"/api/v1/audit-logs?action=PAYMENT_RECORDED&entityId={invoice.InvoiceId}", admin);
            Assert.Equal(200, audit.Code);
            Assert.Equal(1, audit.Prop("totalCount").GetInt32());

            var type = OutboxMessageSerializer.TypeNameOf(typeof(PaymentRecorded));
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            OutboxMessageStatus? status = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                await using var scope = fixture.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var message = await db.Set<OutboxMessage>().AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Type == type && m.Payload.Contains(paymentId));

                status = message?.Status;

                if (status == OutboxMessageStatus.Processed)
                {
                    break;
                }

                await Task.Delay(200);
            }

            Assert.Equal(OutboxMessageStatus.Processed, status);
        }
    }

}
