using UniCore.Application.Modules.Reporting.Reports;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // One invoice paid in three payments by different methods. After each step the invoice, the invoice summary report,
    // the collections report and the outstanding-balances report must all tell the same story. The reports are read as
    // before/after differences because the database is shared with other tests.
    [Collection(ApiCollection.Name)]
    public sealed class FinanceReconciliationJourneyApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string Today(int offsetDays = 0) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offsetDays).ToString("yyyy-MM-dd");

        private static string Range => $"from={Today(-1)}&to={Today(1)}";

        private Task<ApiResponse> PayAsync(string token, Guid invoice, decimal amount, string method, string? key = null)
            => Client.PostAsync($"/api/v1/invoices/{invoice}/payments", new { amount, method, reference = method is "BankTransfer" or "Cheque" ? $"REF-{Guid.NewGuid():N}"[..16] : null }, token, new Dictionary<string, string> { ["Idempotency-Key"] = key ?? $"key-{Guid.NewGuid():N}" });

        private sealed record Totals(int Invoices, decimal Billed, decimal Paid, int Payments, decimal Collected);

        private async Task<Totals> TotalsAsync(string admin)
        {
            var summary = await Client.GetAsync($"/api/v1/reports/{FinanceInvoiceSummaryReport.Code}/run?{Range}", admin);
            Assert.Equal(200, summary.Code);
            var rows = summary.Prop("rows");
            var invoices = rows[rows.GetArrayLength() - 1];

            var collections = await Client.GetAsync($"/api/v1/reports/{FinanceCollectionsReport.Code}/run?{Range}", admin);
            Assert.Equal(200, collections.Code);
            var collectionRows = collections.Prop("rows");

            // With no payment in the range the report has no rows at all (not even a Total row).
            var payments = 0;
            var collected = 0m;
            if (collectionRows.GetArrayLength() > 0)
            {
                var total = collectionRows[collectionRows.GetArrayLength() - 1];
                payments = total[2].GetInt32();
                collected = total[3].GetDecimal();
            }

            return new Totals(invoices[1].GetInt32(), invoices[2].GetDecimal(), invoices[3].GetDecimal(), payments, collected);
        }

        private async Task<List<decimal>> OutstandingBalancesAsync(string admin)
        {
            var run = await Client.GetAsync($"/api/v1/reports/{FinanceOutstandingBalancesReport.Code}/run?pageSize=100", admin);
            Assert.Equal(200, run.Code);

            return [.. Enumerable.Range(0, run.Prop("rows").GetArrayLength()).Select(i => run.Prop("rows")[i][2].GetDecimal())];
        }

        [Fact]
        public async Task Journey_three_payments_by_three_methods_and_every_report_agrees_with_the_invoice()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 777_123m);
            var before = await TotalsAsync(admin);

            // Issued, nothing paid: the whole amount is outstanding.
            Assert.Contains(777_123m, await OutstandingBalancesAsync(admin));

            // 1. Cash.
            Assert.Equal(201, (await PayAsync(admin, invoice.InvoiceId, 300_000m, "Cash")).Code);
            var afterCash = await Client.GetAsync($"/api/v1/invoices/{invoice.InvoiceId}", admin);
            Assert.Equal("PartiallyPaid", afterCash.Str("status"));
            Assert.Equal(477_123m, afterCash.Prop("balance").GetDecimal());

            var balances = await OutstandingBalancesAsync(admin);
            Assert.Contains(477_123m, balances);
            Assert.DoesNotContain(777_123m, balances);

            // 2. Card, sent twice with the same key: the replay must not count twice.
            var key = $"key-{Guid.NewGuid():N}";
            var card = await PayAsync(admin, invoice.InvoiceId, 200_000m, "Card", key);
            Assert.Equal(201, card.Code);
            var replay = await PayAsync(admin, invoice.InvoiceId, 200_000m, "Card", key);
            Assert.True(replay.Code is 200 or 201);

            var afterCard = await Client.GetAsync($"/api/v1/invoices/{invoice.InvoiceId}", admin);
            Assert.Equal(500_000m, afterCard.Prop("paidAmount").GetDecimal());
            Assert.Equal(277_123m, afterCard.Prop("balance").GetDecimal());
            Assert.Equal(2, afterCard.Prop("payments").GetArrayLength());

            // 3. More than the balance is refused and changes nothing.
            var over = await PayAsync(admin, invoice.InvoiceId, 300_000m, "BankTransfer");
            Assert.Equal(422, over.Code);
            Assert.Equal("PAYMENT_EXCEEDS_BALANCE", over.ErrorCode);
            Assert.Equal(277_123m, (await Client.GetAsync($"/api/v1/invoices/{invoice.InvoiceId}", admin)).Prop("balance").GetDecimal());

            // 4. The exact balance by bank transfer closes the invoice.
            Assert.Equal(201, (await PayAsync(admin, invoice.InvoiceId, 277_123m, "BankTransfer")).Code);
            var paid = await Client.GetAsync($"/api/v1/invoices/{invoice.InvoiceId}", admin);
            Assert.Equal("Paid", paid.Str("status"));
            Assert.Equal(0m, paid.Prop("balance").GetDecimal());
            Assert.Equal(3, paid.Prop("payments").GetArrayLength());

            // The reports moved by exactly this invoice and these three payments.
            var after = await TotalsAsync(admin);
            Assert.Equal(before.Invoices, after.Invoices);
            Assert.Equal(before.Billed, after.Billed);
            Assert.Equal(before.Paid + 777_123m, after.Paid);
            Assert.Equal(before.Payments + 3, after.Payments);
            Assert.Equal(before.Collected + 777_123m, after.Collected);

            // A paid invoice is no longer outstanding, at any of its earlier balances.
            var finalBalances = await OutstandingBalancesAsync(admin);
            Assert.DoesNotContain(777_123m, finalBalances);
            Assert.DoesNotContain(477_123m, finalBalances);
            Assert.DoesNotContain(277_123m, finalBalances);
        }
    }
}
