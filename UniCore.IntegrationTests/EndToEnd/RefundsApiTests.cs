using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class RefundsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewKey() => $"key-{Guid.NewGuid():N}";

        private async Task<string> OfficerTokenAsync()
        {
            // FinanceOfficer is a university-wide role, so it needs the Global scope.
            var officer = await _api.CreateUserAsync("refofficer", role: RoleNames.FinanceOfficer, scopeType: ScopeType.Global);
            return await _api.LoginTokenAsync(officer.UserName, officer.Password);
        }

        // An invoice of 1000 with one payment. Returns the invoice id and the payment id.
        private async Task<(Guid Invoice, Guid Payment)> PaidInvoiceAsync(string admin, decimal paid)
        {
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);

            var payment = await Client.PostAsync(
                $"/api/v1/invoices/{invoice.InvoiceId}/payments",
                new { amount = paid, method = "Cash" },
                admin,
                new Dictionary<string, string> { ["Idempotency-Key"] = NewKey() });

            Assert.Equal(201, payment.Code);
            return (invoice.InvoiceId, payment.GuidOf("id"));
        }

        private Task<ApiResponse> RequestAsync(string token, Guid payment, decimal? amount, string? reason = "Paid twice")
            => Client.PostAsync("/api/v1/refunds", new { paymentId = payment, amount, reason }, token);

        [Fact]
        public async Task A_refund_goes_through_request_approval_and_processing_and_the_invoice_follows()
        {
            var admin = await _api.AdminTokenAsync();
            var officer = await OfficerTokenAsync();
            var (invoice, payment) = await PaidInvoiceAsync(admin, 600m);

            var requested = await RequestAsync(officer, payment, 200m);
            Assert.Equal(201, requested.Code);
            Assert.Equal("Requested", requested.Str("status"));
            var refund = requested.GuidOf("id");

            // Reserved on the payment, but the invoice has not changed yet.
            var reserved = await Client.GetAsync($"/api/v1/invoices/{invoice}", admin);
            Assert.Equal(600m, reserved.Prop("paidAmount").GetDecimal());
            Assert.Equal(200m, reserved.Prop("payments")[0].GetProperty("refundCommittedAmount").GetDecimal());

            var approved = await Client.PostAsync($"/api/v1/refunds/{refund}/approve", null, admin);
            Assert.Equal(200, approved.Code);
            Assert.Equal("Approved", approved.Str("status"));

            var processed = await Client.PostAsync($"/api/v1/refunds/{refund}/process", null, admin);
            Assert.Equal(200, processed.Code);
            Assert.Equal("Processed", processed.Str("status"));

            var after = await Client.GetAsync($"/api/v1/invoices/{invoice}", admin);
            Assert.Equal(400m, after.Prop("paidAmount").GetDecimal());
            Assert.Equal(600m, after.Prop("balance").GetDecimal());
            Assert.Equal("PartiallyPaid", after.Str("status"));
            Assert.Equal(200m, after.Prop("payments")[0].GetProperty("refundedAmount").GetDecimal());

            var fetched = await Client.GetAsync($"/api/v1/refunds/{refund}", admin);
            Assert.Equal(200, fetched.Code);

            var list = await Client.GetAsync($"/api/v1/refunds?invoiceId={invoice}&status=Processed", admin);
            Assert.Equal(1, list.Prop("totalCount").GetInt32());
        }

        [Fact]
        public async Task A_refund_of_the_whole_payment_makes_the_invoice_issued_again()
        {
            var admin = await _api.AdminTokenAsync();
            var officer = await OfficerTokenAsync();
            var (invoice, payment) = await PaidInvoiceAsync(admin, 300m);

            var refund = (await RequestAsync(officer, payment, 300m)).GuidOf("id");
            await Client.PostAsync($"/api/v1/refunds/{refund}/approve", null, admin);
            await Client.PostAsync($"/api/v1/refunds/{refund}/process", null, admin);

            var after = await Client.GetAsync($"/api/v1/invoices/{invoice}", admin);
            Assert.Equal("Issued", after.Str("status"));
            Assert.Equal(0m, after.Prop("paidAmount").GetDecimal());
            Assert.Equal(1000m, after.Prop("balance").GetDecimal());
        }

        [Fact]
        public async Task The_person_who_asked_cannot_approve_the_refund()
        {
            var admin = await _api.AdminTokenAsync();
            var (_, payment) = await PaidInvoiceAsync(admin, 600m);

            var refund = (await RequestAsync(admin, payment, 100m)).GuidOf("id");
            var approve = await Client.PostAsync($"/api/v1/refunds/{refund}/approve", null, admin);

            Assert.Equal(422, approve.Code);
            Assert.Equal("REFUND_SELF_APPROVAL", approve.ErrorCode);
            Assert.Equal("Requested", (await Client.GetAsync($"/api/v1/refunds/{refund}", admin)).Str("status"));
        }

        [Fact]
        public async Task A_refund_cannot_be_processed_before_it_is_approved()
        {
            var admin = await _api.AdminTokenAsync();
            var officer = await OfficerTokenAsync();
            var (_, payment) = await PaidInvoiceAsync(admin, 600m);
            var refund = (await RequestAsync(officer, payment, 100m)).GuidOf("id");

            var process = await Client.PostAsync($"/api/v1/refunds/{refund}/process", null, admin);

            Assert.Equal(422, process.Code);
            Assert.Equal("REFUND_INVALID_STATE", process.ErrorCode);
        }

        [Fact]
        public async Task Refunds_cannot_go_over_the_payment_and_a_rejection_frees_the_amount()
        {
            var admin = await _api.AdminTokenAsync();
            var officer = await OfficerTokenAsync();
            var (_, payment) = await PaidInvoiceAsync(admin, 600m);

            var over = await RequestAsync(officer, payment, 600.01m);
            Assert.Equal(422, over.Code);
            Assert.Equal("REFUND_EXCEEDS_PAYMENT", over.ErrorCode);

            var first = (await RequestAsync(officer, payment, 400m)).GuidOf("id");

            var second = await RequestAsync(officer, payment, 300m);
            Assert.Equal("REFUND_EXCEEDS_PAYMENT", second.ErrorCode);

            var noReason = await Client.PostAsync($"/api/v1/refunds/{first}/reject", new { reason = "  " }, admin);
            Assert.Equal(400, noReason.Code);
            Assert.Equal("REFUND_DATA_INVALID", noReason.ErrorCode);

            var rejected = await Client.PostAsync($"/api/v1/refunds/{first}/reject", new { reason = "Not eligible" }, admin);
            Assert.Equal(200, rejected.Code);
            Assert.Equal("Rejected", rejected.Str("status"));
            Assert.Equal("Not eligible", rejected.Str("rejectionReason"));

            // The 400 is free again.
            Assert.Equal(201, (await RequestAsync(officer, payment, 600m)).Code);

            var approveRejected = await Client.PostAsync($"/api/v1/refunds/{first}/approve", null, admin);
            Assert.Equal("REFUND_INVALID_STATE", approveRejected.ErrorCode);
        }

        [Fact]
        public async Task Two_parallel_refund_requests_cannot_both_take_the_last_of_the_payment()
        {
            var admin = await _api.AdminTokenAsync();
            var officer = await OfficerTokenAsync();
            var (_, payment) = await PaidInvoiceAsync(admin, 600m);

            var results = await Task.WhenAll(
                RequestAsync(officer, payment, 400m),
                RequestAsync(officer, payment, 400m));

            Assert.Equal(1, results.Count(r => r.Code == 201));
            Assert.All(results.Where(r => r.Code != 201), r => Assert.Contains(r.ErrorCode ?? string.Empty, new[] { "CONCURRENCY_CONFLICT", "REFUND_EXCEEDS_PAYMENT" }));
        }

        [Fact]
        public async Task Bad_input_and_missing_permissions_are_refused()
        {
            var admin = await _api.AdminTokenAsync();
            var user = await _api.CreateUserAsync("refnoperm");
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);
            var (_, payment) = await PaidInvoiceAsync(admin, 600m);

            Assert.Equal("REFUND_DATA_INVALID", (await RequestAsync(admin, payment, 10m, reason: "  ")).ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_REQUIRED", (await RequestAsync(admin, payment, null)).ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_PRECISION_INVALID", (await RequestAsync(admin, payment, 10.123m)).ErrorCode);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", (await RequestAsync(admin, payment, 0m)).ErrorCode);

            var unknown = await RequestAsync(admin, Guid.NewGuid(), 10m);
            Assert.Equal(404, unknown.Code);
            Assert.Equal("PAYMENT_NOT_FOUND", unknown.ErrorCode);

            Assert.Equal(403, (await RequestAsync(token, payment, 10m)).Code);
            Assert.Equal(403, (await Client.GetAsync("/api/v1/refunds", token)).Code);
            Assert.Equal(404, (await Client.GetAsync($"/api/v1/refunds/{Guid.NewGuid()}", admin)).Code);
        }

        [Fact]
        public async Task Every_step_of_a_refund_is_audited()
        {
            var admin = await _api.AdminTokenAsync();
            var officer = await OfficerTokenAsync();
            var (_, payment) = await PaidInvoiceAsync(admin, 600m);
            var refund = (await RequestAsync(officer, payment, 100m)).GuidOf("id");
            await Client.PostAsync($"/api/v1/refunds/{refund}/approve", null, admin);
            await Client.PostAsync($"/api/v1/refunds/{refund}/process", null, admin);

            foreach (var action in new[] { "REFUND_REQUESTED", "REFUND_APPROVED", "REFUND_PROCESSED" })
            {
                var audit = await Client.GetAsync($"/api/v1/audit-logs?action={action}&entityId={refund}", admin);
                Assert.Equal(1, audit.Prop("totalCount").GetInt32());
            }
        }
    }


}
