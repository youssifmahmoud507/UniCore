using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Dashboard;
using UniCore.Application.Modules.Reporting.Reports;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Reporting
{
    // Rules every report and every widget must keep, checked on the real ones. A new report that breaks one fails here.
    public class ReportCatalogTests
    {
        private static List<IReportProvider> Providers(ReportingEnv? shared = null)
        {
            var env = shared ?? new ReportingEnv();
            var scopes = new ReportScopeResolver(env.Access, env.Hierarchy);

            return
            [
                new FinanceInvoiceSummaryReport(env.Data),
                new FinanceOutstandingBalancesReport(env.Data, env.Clock),
                new FinanceCollectionsReport(env.Data),
                new WorkflowRequestsReport(env.Data, scopes, env.Clock),
                new DocumentsByTypeReport(env.Data),
                new NotificationDeliveryReport(env.Data, env.Clock),
                new AuditActivityReport(env.Data, env.Clock)
            ];
        }

        private static List<IDashboardWidget> Widgets()
        {
            var env = new ReportingEnv();
            var scopes = new ReportScopeResolver(env.Access, env.Hierarchy);

            return
            [
                new FinanceDashboardWidget(env.Data, env.Clock),
                new RequestsDashboardWidget(env.Data, scopes, env.Clock),
                new DocumentsDashboardWidget(env.Data, env.Clock),
                new SecurityDashboardWidget(env.Data, env.Clock),
                new NotificationsDashboardWidget(env.Data, env.Clock)
            ];
        }

        [Fact]
        public void Report_codes_are_unique_upper_case_with_underscores()
        {
            var codes = Providers().Select(p => p.Descriptor.Code).ToList();

            Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(codes, code => Assert.True(code.All(c => c is (>= 'A' and <= 'Z') or '_'), $"{code} must be UPPER_CASE"));
        }

        [Fact]
        public void Every_report_needs_a_permission_that_exists()
        {
            foreach (var provider in Providers())
            {
                Assert.True(Permissions.AllCodes.Contains(provider.Descriptor.Permission), $"{provider.Descriptor.Code}: unknown permission {provider.Descriptor.Permission}");
            }
        }

        [Fact]
        public void Every_report_has_unique_parameter_names_and_column_keys_and_a_choice_list_where_needed()
        {
            foreach (var provider in Providers())
            {
                var d = provider.Descriptor;

                Assert.Equal(d.Parameters.Count, d.Parameters.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
                Assert.Equal(d.Columns.Count, d.Columns.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count());
                Assert.NotEmpty(d.Columns);
                Assert.All(d.Parameters.Where(p => p.Type == ReportParameterType.Choice), p => Assert.NotEmpty(p.Choices ?? []));
                Assert.False(string.IsNullOrWhiteSpace(d.Name));
                Assert.False(string.IsNullOrWhiteSpace(d.Description));
            }
        }

        [Fact]
        public void Money_and_student_reports_that_name_people_are_marked_sensitive()
        {
            var byCode = Providers().ToDictionary(p => p.Descriptor.Code, p => p.Descriptor);

            Assert.True(byCode[FinanceOutstandingBalancesReport.Code].IsSensitive);
            Assert.True(byCode[AuditActivityReport.Code].IsSensitive);
        }

        [Fact]
        public void The_catalog_finds_a_report_in_any_letter_case_and_lists_them_in_a_stable_order()
        {
            var catalog = new ReportCatalog(Providers());

            Assert.NotNull(catalog.Find("finance_invoice_summary"));
            Assert.NotNull(catalog.Find("  FINANCE_COLLECTIONS "));
            Assert.Null(catalog.Find("NOPE"));
            Assert.Null(catalog.Find(null));
            Assert.Equal(catalog.All.Select(p => p.Descriptor.Category).OrderBy(c => c, StringComparer.Ordinal), catalog.All.Select(p => p.Descriptor.Category));
        }

        [Fact]
        public void Widget_codes_are_unique_and_each_needs_a_permission_that_exists()
        {
            var widgets = Widgets();

            Assert.Equal(widgets.Count, widgets.Select(w => w.Code).Distinct(StringComparer.Ordinal).Count());
            Assert.All(widgets, w => Assert.True(Permissions.AllCodes.Contains(w.Permission), $"{w.Code}: unknown permission"));
        }

        [Fact]
        public async Task Every_report_returns_rows_as_wide_as_its_columns()
        {
            var env = new ReportingEnv().Grant(Permissions.AllCodes);
            var invoice = new InvoiceFact(Guid.NewGuid(), "S1", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), UniCore.Domain.Modules.Finance.InvoiceStatus.Issued, 100m, 0m, 100m);
            env.Data.Invoices.Add(invoice);
            env.Data.Payments.Add(new PaymentFact(new DateOnly(2026, 11, 2), UniCore.Domain.Modules.Finance.PaymentMethod.Cash, 10m, 0m));
            env.Data.Workflows.Add(new WorkflowFact("W", UniCore.Domain.Modules.Workflow.WorkflowInstanceStatus.InProgress, null, null, ReportingEnv.Now.AddDays(-1), null));
            env.Data.Documents.Add(new DocumentStat("T", "Type", false, UniCore.Domain.Modules.Documents.DocumentStatus.Active, 1, 100));
            env.Data.Deliveries.Add(new DeliveryStat(UniCore.Domain.Modules.Notifications.NotificationChannel.Email, UniCore.Domain.Modules.Notifications.NotificationDeliveryStatus.Sent, 1));
            env.Data.Audit.Add(new AuditStat("X", 1, ReportingEnv.Now));
            var collections = new ReportArguments(new Dictionary<string, object> { ["from"] = new DateOnly(2026, 11, 1), ["to"] = new DateOnly(2026, 11, 8) });

            foreach (var provider in Providers(env))
            {
                var ran = await provider.RunAsync(provider is FinanceCollectionsReport ? collections : ReportArguments.None, ReportPage.First(50));

                Assert.True(ran.TryGetValue(out var table), $"{provider.Descriptor.Code} failed");
                Assert.NotEmpty(table!.Rows);
                Assert.All(table.Rows, row => Assert.Equal(provider.Descriptor.Columns.Count, row.Count));
                Assert.Equal(provider.Descriptor.Columns, table.Columns);
            }
        }
    }




}
