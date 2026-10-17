using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Modules.Finance;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class FinancialHoldsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        // Seen from 60 days ahead, every invoice of the scenarios (due in 30 days) is overdue.
        private static DateOnly Future => AdmitSpec.Today.AddDays(60);

        private static string NewKey() => $"key-{Guid.NewGuid():N}";

        private Task<ApiResponse> PlaceAsync(string token, Guid student, string[]? areas, string? note = "Library fine unpaid")
            => Client.PostAsync("/api/v1/financial-holds", new { studentId = student, areas, note }, token);

        private Task<ApiResponse> FinanceAsync(string token, Guid student) => Client.GetAsync($"/api/v1/students/{student}/finance", token);

        private async Task<HoldEvaluationResult> EvaluateAsOfFutureAsync(Guid student)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var evaluator = scope.ServiceProvider.GetRequiredService<FinancialHoldEvaluator>();

            var result = await evaluator.EvaluateStudentAsync(student, Future);

            Assert.True(result.TryGetValue(out var outcome));
            Assert.NotNull(outcome);
            return outcome;
        }

        private Task<ApiResponse> PayAsync(string token, Guid invoice, decimal amount)
            => Client.PostAsync(
                $"/api/v1/invoices/{invoice}/payments",
                new { amount, method = "Cash" },
                token,
                new Dictionary<string, string> { ["Idempotency-Key"] = NewKey() });

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await condition())
                {
                    return true;
                }

                await Task.Delay(200);
            }

            return await condition();
        }

        // ---------- manual holds ----------

        [Fact]
        public async Task A_manual_hold_blocks_only_its_areas_and_the_eligibility_service_says_so()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 500m);

            var placed = await PlaceAsync(admin, prepared.StudentId, ["Services"]);
            Assert.Equal(201, placed.Code);
            Assert.Equal("Manual", placed.Str("reason"));
            Assert.Equal("Active", placed.Str("status"));

            var finance = await FinanceAsync(admin, prepared.StudentId);
            Assert.Equal(200, finance.Code);
            Assert.True(finance.Prop("blocksServices").GetBoolean());
            Assert.False(finance.Prop("blocksRegistration").GetBoolean());
            Assert.False(finance.Prop("blocksGraduation").GetBoolean());
            Assert.Equal(1, finance.Prop("activeHolds").GetArrayLength());

            await using var scope = fixture.Services.CreateAsyncScope();
            var eligibility = scope.ServiceProvider.GetRequiredService<IFinancialEligibilityService>();

            Assert.True((await eligibility.HasBlockingHoldAsync(prepared.StudentId)).Value);
            Assert.True((await eligibility.HasBlockingHoldAsync(prepared.StudentId, FinancialHoldArea.Services)).Value);
            Assert.False((await eligibility.HasBlockingHoldAsync(prepared.StudentId, FinancialHoldArea.Registration)).Value);
            Assert.False((await eligibility.HasBlockingHoldAsync(prepared.StudentId, FinancialHoldArea.Graduation)).Value);
            Assert.False((await eligibility.HasBlockingHoldAsync(Guid.NewGuid())).Value);
        }

        [Fact]
        public async Task A_hold_is_released_with_a_note_and_only_once()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 500m);
            var hold = (await PlaceAsync(admin, prepared.StudentId, ["Registration", "Graduation"])).GuidOf("id");

            var noNote = await Client.PostAsync($"/api/v1/financial-holds/{hold}/release", new { note = "  " }, admin);
            Assert.Equal(400, noNote.Code);
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", noNote.ErrorCode);

            var released = await Client.PostAsync($"/api/v1/financial-holds/{hold}/release", new { note = "Paid at the desk" }, admin);
            Assert.Equal(200, released.Code);
            Assert.Equal("Released", released.Str("status"));
            Assert.Equal("Paid at the desk", released.Str("releaseNote"));

            var finance = await FinanceAsync(admin, prepared.StudentId);
            Assert.False(finance.Prop("blocksRegistration").GetBoolean());
            Assert.Equal(0, finance.Prop("activeHolds").GetArrayLength());

            var again = await Client.PostAsync($"/api/v1/financial-holds/{hold}/release", new { note = "Again" }, admin);
            Assert.Equal(422, again.Code);
            Assert.Equal("FINANCIAL_HOLD_NOT_ACTIVE", again.ErrorCode);
        }

        [Fact]
        public async Task Bad_input_and_missing_permissions_are_refused()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 500m);
            var user = await _api.CreateUserAsync("holdnoperm");
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", (await PlaceAsync(admin, prepared.StudentId, null)).ErrorCode);
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", (await PlaceAsync(admin, prepared.StudentId, [])).ErrorCode);
            Assert.Equal("FINANCIAL_HOLD_DATA_INVALID", (await PlaceAsync(admin, prepared.StudentId, ["Services"], note: null)).ErrorCode);

            var unknown = await PlaceAsync(admin, Guid.NewGuid(), ["Services"]);
            Assert.Equal(404, unknown.Code);
            Assert.Equal("STUDENT_NOT_FOUND", unknown.ErrorCode);

            Assert.Equal(403, (await PlaceAsync(token, prepared.StudentId, ["Services"])).Code);
            Assert.Equal(403, (await Client.GetAsync("/api/v1/financial-holds", token)).Code);
            Assert.Equal(403, (await FinanceAsync(token, prepared.StudentId)).Code);
            Assert.Equal(403, (await Client.PostAsync("/api/v1/financial-holds/evaluate", new { }, token)).Code);
            Assert.Equal(404, (await FinanceAsync(admin, Guid.NewGuid())).Code);
            Assert.Equal(404, (await Client.GetAsync($"/api/v1/financial-holds/{Guid.NewGuid()}", admin)).Code);
        }

        [Fact]
        public async Task Holds_can_be_listed_by_student_and_status()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 500m);
            var first = (await PlaceAsync(admin, prepared.StudentId, ["Services"])).GuidOf("id");
            await PlaceAsync(admin, prepared.StudentId, ["Registration"]);
            await Client.PostAsync($"/api/v1/financial-holds/{first}/release", new { note = "Done" }, admin);

            var active = await Client.GetAsync($"/api/v1/financial-holds?studentId={prepared.StudentId}&status=Active", admin);
            var released = await Client.GetAsync($"/api/v1/financial-holds?studentId={prepared.StudentId}&status=Released&reason=Manual", admin);

            Assert.Equal(1, active.Prop("totalCount").GetInt32());
            Assert.Equal(1, released.Prop("totalCount").GetInt32());
        }

        // ---------- the balance ----------

        [Fact]
        public async Task The_balance_counts_pending_charges_invoices_and_payments()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 1000m);

            var pending = (await FinanceAsync(admin, prepared.StudentId)).Prop("balance");
            Assert.Equal(1000m, pending.GetProperty("totalCharged").GetDecimal());
            Assert.Equal(0m, pending.GetProperty("totalPaid").GetDecimal());
            Assert.Equal(1000m, pending.GetProperty("outstanding").GetDecimal());

            var invoice = await Client.PostAsync("/api/v1/invoices", new { studentId = prepared.StudentId, academicSemesterId = prepared.Semester }, admin);
            Assert.Equal(201, invoice.Code);

            var invoiced = (await FinanceAsync(admin, prepared.StudentId)).Prop("balance");
            Assert.Equal(1000m, invoiced.GetProperty("totalCharged").GetDecimal());

            Assert.Equal(201, (await PayAsync(admin, invoice.GuidOf("id"), 400m)).Code);

            var paid = (await FinanceAsync(admin, prepared.StudentId)).Prop("balance");
            Assert.Equal(1000m, paid.GetProperty("totalCharged").GetDecimal());
            Assert.Equal(400m, paid.GetProperty("totalPaid").GetDecimal());
            Assert.Equal(600m, paid.GetProperty("outstanding").GetDecimal());
        }

        // ---------- automatic holds ----------

        [Fact]
        public async Task An_overdue_invoice_places_an_automatic_hold_once_and_paying_it_releases_the_hold()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);

            // Nothing is overdue today, so the real evaluation does nothing.
            var today = await Client.PostAsync("/api/v1/financial-holds/evaluate", new { studentId = invoice.StudentId }, admin);
            Assert.Equal(200, today.Code);
            Assert.Equal(0, today.Prop("placed").GetInt32());

            // Seen from 60 days ahead, the invoice is overdue.
            var placed = await EvaluateAsOfFutureAsync(invoice.StudentId);
            Assert.Equal(1, placed.Placed);

            var finance = await FinanceAsync(admin, invoice.StudentId);
            Assert.True(finance.Prop("blocksRegistration").GetBoolean());
            Assert.True(finance.Prop("blocksGraduation").GetBoolean());
            Assert.True(finance.Prop("blocksServices").GetBoolean());

            var hold = finance.Prop("activeHolds")[0];
            Assert.Equal("OverdueBalance", hold.GetProperty("reason").GetString());
            Assert.Equal(1000m, hold.GetProperty("overdueAmount").GetDecimal());
            Assert.Equal(3, hold.GetProperty("areas").GetArrayLength());

            // Evaluating again changes nothing: one automatic hold per student.
            var again = await EvaluateAsOfFutureAsync(invoice.StudentId);
            Assert.Equal(0, again.Placed);

            var list = await Client.GetAsync($"/api/v1/financial-holds?studentId={invoice.StudentId}&status=Active", admin);
            Assert.Equal(1, list.Prop("totalCount").GetInt32());

            // Paying everything releases it by itself (PaymentRecorded through the outbox).
            Assert.Equal(201, (await PayAsync(admin, invoice.InvoiceId, 1000m)).Code);

            var released = await WaitUntilAsync(
                async () => !(await FinanceAsync(admin, invoice.StudentId)).Prop("blocksRegistration").GetBoolean(),
                TimeSpan.FromSeconds(30));

            Assert.True(released);

            var history = await Client.GetAsync($"/api/v1/financial-holds?studentId={invoice.StudentId}&status=Released", admin);
            var item = history.Prop("items")[0];
            Assert.Equal("OverdueBalance", item.GetProperty("reason").GetString());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("releasedByUserId").ValueKind);
        }

        [Fact]
        public async Task A_person_who_releases_an_automatic_hold_waives_it_for_a_while()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);
            Assert.Equal(1, (await EvaluateAsOfFutureAsync(invoice.StudentId)).Placed);

            var hold = (await FinanceAsync(admin, invoice.StudentId)).Prop("activeHolds")[0].GetProperty("id").GetString();
            var released = await Client.PostAsync($"/api/v1/financial-holds/{hold}/release", new { note = "Payment plan agreed" }, admin);
            Assert.Equal(200, released.Code);

            // Still overdue as of the future date, but the waiver keeps the hold from coming back.
            Assert.Equal(0, (await EvaluateAsOfFutureAsync(invoice.StudentId)).Placed);

            var finance = await FinanceAsync(admin, invoice.StudentId);
            Assert.False(finance.Prop("blocksRegistration").GetBoolean());
        }

        [Fact]
        public async Task A_manual_hold_is_not_touched_by_the_evaluation_or_by_a_payment()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 500m);
            var manual = await PlaceAsync(admin, invoice.StudentId, ["Registration"]);
            Assert.Equal(201, manual.Code);

            var evaluated = await EvaluateAsOfFutureAsync(invoice.StudentId);
            Assert.Equal(1, evaluated.Placed);
            Assert.Equal(0, evaluated.Released);

            Assert.Equal(201, (await PayAsync(admin, invoice.InvoiceId, 500m)).Code);

            // The automatic one goes (nothing overdue any more); the manual one stays.
            var settled = await WaitUntilAsync(
                async () => (await FinanceAsync(admin, invoice.StudentId)).Prop("activeHolds").GetArrayLength() == 1,
                TimeSpan.FromSeconds(30));

            Assert.True(settled);

            var left = (await FinanceAsync(admin, invoice.StudentId)).Prop("activeHolds")[0];
            Assert.Equal("Manual", left.GetProperty("reason").GetString());
        }

        [Fact]
        public async Task Evaluating_everybody_includes_the_students_that_owe_overdue_money()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);

            await using var scope = fixture.Services.CreateAsyncScope();
            var runner = scope.ServiceProvider.GetRequiredService<IFinancialHoldRunner>();

            var result = await runner.RunAsync(Future);

            Assert.True(result.TryGetValue(out var outcome));
            Assert.NotNull(outcome);
            Assert.True(outcome.Placed >= 1);
            Assert.Equal(0, outcome.Failed);

            var finance = await FinanceAsync(admin, invoice.StudentId);
            Assert.True(finance.Prop("blocksRegistration").GetBoolean());
        }

        [Fact]
        public async Task Hold_changes_are_audited()
        {
            var admin = await _api.AdminTokenAsync();
            var prepared = await InvoiceScenario.PrepareAsync(_api, admin, 500m);
            var hold = (await PlaceAsync(admin, prepared.StudentId, ["Services"])).GuidOf("id");
            await Client.PostAsync($"/api/v1/financial-holds/{hold}/release", new { note = "Done" }, admin);

            var placed = await Client.GetAsync($"/api/v1/audit-logs?action=FINANCIAL_HOLD_PLACED&entityId={hold}", admin);
            var released = await Client.GetAsync($"/api/v1/audit-logs?action=FINANCIAL_HOLD_RELEASED&entityId={hold}", admin);

            Assert.Equal(1, placed.Prop("totalCount").GetInt32());
            Assert.Equal(1, released.Prop("totalCount").GetInt32());
        }
    }



}
