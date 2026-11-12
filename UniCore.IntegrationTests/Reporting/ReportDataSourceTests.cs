using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Modules.Audit;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Reporting
{
    // The read-only queries against the real database. Mostly: every query is translated by EF and runs.
    [Collection(ApiCollection.Name)]
    public sealed class ReportDataSourceTests(ApiFixture fixture)
    {
        private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-2);
        private static readonly DateTimeOffset ToExclusive = DateTimeOffset.UtcNow.AddDays(1);

        private async Task<T> InScopeAsync<T>(Func<IReportDataSource, Task<T>> action)
        {
            await using var scope = fixture.Services.CreateAsyncScope();

            return await action(scope.ServiceProvider.GetRequiredService<IReportDataSource>());
        }

        [Fact]
        public async Task Every_query_runs_against_the_database()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            Assert.True((await InScopeAsync(d => d.GetInvoicesAsync(null, null))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetInvoicesAsync(today.AddDays(-30), today))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetOpenInvoicesAsync())).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetPaymentsAsync(today.AddDays(-30), today))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetWorkflowsAsync(null, null))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetWorkflowsAsync(From, ToExclusive))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetWorkflowStatsAsync(null))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetWorkflowStatsAsync(From))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetDocumentStatsAsync(null, null))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetDocumentStatsAsync(From, ToExclusive))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetDeliveryStatsAsync(From, ToExclusive))).IsSuccess);
            Assert.True((await InScopeAsync(d => d.GetAuditStatsAsync(From, ToExclusive))).IsSuccess);
        }

        [Fact]
        public async Task Audit_stats_count_what_happened_in_the_window()
        {
            await fixture.Api.CreateUserAsync("statsuser");

            var stats = await InScopeAsync(d => d.GetAuditStatsAsync(From, ToExclusive));

            Assert.True(stats.TryGetValue(out var rows));
            var login = rows!.Single(r => r.Action == AuditActions.AuthLoginSucceeded);
            Assert.True(login.Count >= 1);
            Assert.True(login.LastAt >= From);
            Assert.Equal(rows.Count, rows.Select(r => r.Action).Distinct().Count());
        }

        [Fact]
        public async Task An_empty_window_has_no_audit_stats()
        {
            var stats = await InScopeAsync(d => d.GetAuditStatsAsync(From.AddYears(-30), From.AddYears(-29)));

            Assert.True(stats.IsSuccess);
            Assert.Empty(stats.Value!);
        }

        [Fact]
        public async Task Invoices_are_read_with_their_student_number_and_amounts()
        {
            var admin = await fixture.Api.AdminTokenAsync();
            var issued = await InvoiceScenario.IssueAsync(fixture.Api, admin, 321m);

            var invoices = await InScopeAsync(d => d.GetInvoicesAsync(null, null));

            Assert.True(invoices.TryGetValue(out var facts));
            var mine = facts!.Where(f => f.StudentId == issued.StudentId).ToList();
            Assert.Single(mine);
            Assert.Equal(321m, mine[0].Total);
            Assert.Equal(321m, mine[0].Balance);
            Assert.Equal(0m, mine[0].Paid);
            Assert.False(string.IsNullOrWhiteSpace(mine[0].StudentNumber));
        }

        [Fact]
        public async Task Open_invoices_include_the_unpaid_one_with_its_balance()
        {
            var admin = await fixture.Api.AdminTokenAsync();
            await InvoiceScenario.IssueAsync(fixture.Api, admin, 4_321_098m);

            var open = await InScopeAsync(d => d.GetOpenInvoicesAsync());

            Assert.True(open.TryGetValue(out var facts));
            Assert.Contains(facts!, f => f.Balance == 4_321_098m);
        }
    }




}
