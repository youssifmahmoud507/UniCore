using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Dashboard;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Notifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.UnitTests.Modules.Reporting
{
    public class DashboardTests
    {
        private static readonly DateOnly Today = new(2026, 11, 8);
        private static readonly DateTimeOffset Now = ReportingEnv.Now;

        private static decimal Metric(IReadOnlyList<DashboardMetric> metrics, string key) => metrics.Single(m => m.Key == key).Value;

        [Fact]
        public void Finance_numbers_add_up_open_invoices_overdue_ones_and_this_months_net_collections()
        {
            var metrics = FinanceDashboardWidget.Build(
                [new OpenInvoiceFact(new DateOnly(2026, 10, 1), 100m), new OpenInvoiceFact(Today, 50.5m), new OpenInvoiceFact(new DateOnly(2026, 12, 1), 25m)],
                [new PaymentFact(Today, PaymentMethod.Cash, 500m, 100m), new PaymentFact(Today, PaymentMethod.Card, 40m, 0m)],
                Today);

            Assert.Equal(3m, Metric(metrics, "openInvoices"));
            Assert.Equal(175.5m, Metric(metrics, "outstanding"));
            Assert.Equal(1m, Metric(metrics, "overdueInvoices"));
            Assert.Equal(440m, Metric(metrics, "collectedThisMonth"));
        }

        [Fact]
        public async Task The_finance_widget_reads_this_month_up_to_today()
        {
            var env = new ReportingEnv();

            var loaded = await new FinanceDashboardWidget(env.Data, env.Clock).LoadAsync();

            Assert.True(loaded.IsSuccess);
            Assert.Equal(new DateOnly(2026, 11, 1), env.Data.PaymentRange.From);
            Assert.Equal(Today, env.Data.PaymentRange.To);
        }

        [Fact]
        public void Request_numbers_count_only_the_users_scope()
        {
            var mine = Guid.NewGuid();
            var theirs = Guid.NewGuid();
            var stats = new[]
            {
                new WorkflowStat(WorkflowInstanceStatus.InProgress, ScopeType.Department, mine, 3),
                new WorkflowStat(WorkflowInstanceStatus.InProgress, ScopeType.Department, theirs, 40),
                new WorkflowStat(WorkflowInstanceStatus.Returned, ScopeType.Department, mine, 1),
                new WorkflowStat(WorkflowInstanceStatus.Completed, ScopeType.Department, mine, 7),
                new WorkflowStat(WorkflowInstanceStatus.Rejected, ScopeType.Department, theirs, 9),
                new WorkflowStat(WorkflowInstanceStatus.Rejected, null, null, 2)
            };

            var scoped = RequestsDashboardWidget.Build(stats, new ReportScope(false, new HashSet<Guid> { mine }));
            var everything = RequestsDashboardWidget.Build(stats, ReportScope.Everything);

            Assert.Equal(3m, Metric(scoped, "inProgress"));
            Assert.Equal(1m, Metric(scoped, "returned"));
            Assert.Equal(7m, Metric(scoped, "completedRecently"));
            Assert.Equal(0m, Metric(scoped, "rejectedRecently"));
            Assert.Equal(43m, Metric(everything, "inProgress"));
            Assert.Equal(11m, Metric(everything, "rejectedRecently"));
        }

        [Fact]
        public async Task The_requests_widget_asks_for_the_finished_ones_of_the_last_30_days_and_is_per_user()
        {
            var env = new ReportingEnv().Grant([Permissions.Requests.Read]);
            var widget = new RequestsDashboardWidget(env.Data, new ReportScopeResolver(env.Access, env.Hierarchy), env.Clock);

            await widget.LoadAsync();

            Assert.Equal(Now.AddDays(-30), env.Data.CompletedSince);
            Assert.True(widget.IsPerUser);
        }

        [Fact]
        public void Document_numbers_count_active_sensitive_recent_and_sensitive_reads()
        {
            var metrics = DocumentsDashboardWidget.Build(
                [new DocumentStat("A", "A", false, DocumentStatus.Active, 10, 1), new DocumentStat("B", "B", true, DocumentStatus.Active, 4, 1), new DocumentStat("B", "B", true, DocumentStatus.Deleted, 9, 1)],
                [new DocumentStat("A", "A", false, DocumentStatus.Active, 2, 1)],
                [new AuditStat(AuditActions.DocumentAccessed, 6, Now), new AuditStat("OTHER", 99, Now)]);

            Assert.Equal(14m, Metric(metrics, "activeDocuments"));
            Assert.Equal(4m, Metric(metrics, "sensitiveDocuments"));
            Assert.Equal(2m, Metric(metrics, "uploadedRecently"));
            Assert.Equal(6m, Metric(metrics, "sensitiveReadsRecently"));
        }

        [Fact]
        public void Security_numbers_pick_the_right_audit_actions_and_windows()
        {
            var metrics = SecurityDashboardWidget.Build(
                [new AuditStat(AuditActions.AuthLoginFailed, 12, Now), new AuditStat(AuditActions.AuthAccountLocked, 2, Now), new AuditStat(AuditActions.AuthTokenReuseDetected, 50, Now)],
                [new AuditStat(AuditActions.AuthTokenReuseDetected, 1, Now), new AuditStat(AuditActions.ReportExported, 8, Now), new AuditStat(AuditActions.AuthLoginFailed, 70, Now)]);

            Assert.Equal(12m, Metric(metrics, "failedLogins24h"));
            Assert.Equal(2m, Metric(metrics, "lockedAccounts24h"));
            Assert.Equal(1m, Metric(metrics, "tokenReuse7d"));
            Assert.Equal(8m, Metric(metrics, "reportExports7d"));
        }

        [Fact]
        public void Notification_numbers_split_sent_waiting_and_failed()
        {
            var metrics = NotificationsDashboardWidget.Build(
            [
                new DeliveryStat(NotificationChannel.Email, NotificationDeliveryStatus.Sent, 30),
                new DeliveryStat(NotificationChannel.InApp, NotificationDeliveryStatus.Sent, 5),
                new DeliveryStat(NotificationChannel.Email, NotificationDeliveryStatus.Pending, 2),
                new DeliveryStat(NotificationChannel.Email, NotificationDeliveryStatus.Queued, 3),
                new DeliveryStat(NotificationChannel.Email, NotificationDeliveryStatus.Failed, 4)
            ]);

            Assert.Equal(35m, Metric(metrics, "sent24h"));
            Assert.Equal(5m, Metric(metrics, "waiting24h"));
            Assert.Equal(4m, Metric(metrics, "failed24h"));
        }

        private sealed class StubWidget(string code, string permission, bool perUser = false, Error? fail = null) : IDashboardWidget
        {
            public int Loads { get; private set; }
            public string Code => code;
            public string Title => $"Widget {code}";
            public string Category => "Test";
            public string Permission => permission;
            public bool IsPerUser => perUser;

            public Task<Result<IReadOnlyList<DashboardMetric>>> LoadAsync(CancellationToken cancellationToken = default)
            {
                Loads++;

                return Task.FromResult(fail is { } error
                    ? Result<IReadOnlyList<DashboardMetric>>.Fail(error)
                    : Result<IReadOnlyList<DashboardMetric>>.Ok([new DashboardMetric("count", "Count", Loads)]));
            }
        }

        private static (ReportingEnv Env, FakeCacheService Cache) Env(params string[] permissions)
            => (new ReportingEnv().Grant(permissions), new FakeCacheService());

        [Fact]
        public async Task Only_the_widgets_the_user_may_see_are_shown()
        {
            var (env, cache) = Env(Permissions.Reports.Read, Permissions.Finance.Read);
            var finance = new StubWidget("finance", Permissions.Finance.Read);
            var security = new StubWidget("security", Permissions.Audit.Read);
            var handler = new DashboardHandler(env.Authorizer, [finance, security], cache, env.User, env.Clock);

            var dashboard = await handler.GetAsync();

            Assert.True(dashboard.TryGetValue(out var result));
            Assert.Equal(["finance"], result!.Widgets.Select(w => w.Code));
            Assert.Empty(result.Unavailable);
            Assert.Equal(0, security.Loads);
        }

        [Fact]
        public async Task Without_reports_read_there_is_no_dashboard()
        {
            var (env, cache) = Env(Permissions.Finance.Read);
            var handler = new DashboardHandler(env.Authorizer, [new StubWidget("finance", Permissions.Finance.Read)], cache, env.User, env.Clock);

            Assert.Equal(ErrorType.Forbidden, (await handler.GetAsync()).Errors[0].ErrorType);
        }

        [Fact]
        public async Task A_widget_is_loaded_once_and_then_served_from_the_cache_but_a_per_user_widget_is_cached_per_user()
        {
            var (env, cache) = Env(Permissions.Reports.Read, Permissions.Finance.Read, Permissions.Requests.Read);
            var shared = new StubWidget("finance", Permissions.Finance.Read);
            var personal = new StubWidget("requests", Permissions.Requests.Read, perUser: true);
            var handler = new DashboardHandler(env.Authorizer, [shared, personal], cache, env.User, env.Clock);

            await handler.GetAsync();
            await handler.GetAsync();

            Assert.Equal(1, shared.Loads);
            Assert.Equal(1, personal.Loads);
            Assert.Contains("dashboard:finance", cache.Entries.Keys);
            Assert.Contains($"dashboard:requests:{env.UserId:N}", cache.Entries.Keys);

            var other = new ReportingEnv().Grant([Permissions.Reports.Read, Permissions.Requests.Read]);
            var otherHandler = new DashboardHandler(other.Authorizer, [personal], cache, other.User, other.Clock);

            await otherHandler.GetAsync();

            Assert.Equal(2, personal.Loads);
        }

        [Fact]
        public async Task One_broken_widget_does_not_hide_the_others()
        {
            var (env, cache) = Env(Permissions.Reports.Read, Permissions.Finance.Read, Permissions.Audit.Read);
            var broken = new StubWidget("finance", Permissions.Finance.Read, fail: UniCore.Domain.Modules.Reporting.ReportingErrors.DataUnavailable);
            var fine = new StubWidget("security", Permissions.Audit.Read);
            var handler = new DashboardHandler(env.Authorizer, [broken, fine], cache, env.User, env.Clock);

            var dashboard = await handler.GetAsync();

            Assert.True(dashboard.TryGetValue(out var result));
            Assert.Equal(["security"], result!.Widgets.Select(w => w.Code));
            Assert.Equal(["finance"], result.Unavailable);
        }

        [Fact]
        public async Task Widgets_are_ordered_by_category_then_code_and_carry_their_numbers()
        {
            var (env, cache) = Env(Permissions.Reports.Read, Permissions.Finance.Read, Permissions.Audit.Read);
            var handler = new DashboardHandler(env.Authorizer, [new StubWidget("b", Permissions.Audit.Read), new StubWidget("a", Permissions.Finance.Read)], cache, env.User, env.Clock);

            var dashboard = (await handler.GetAsync()).Value!;

            Assert.Equal(["a", "b"], dashboard.Widgets.Select(w => w.Code));
            Assert.Equal(1m, dashboard.Widgets[0].Metrics[0].Value);
            Assert.Equal(ReportingEnv.Now, dashboard.Widgets[0].GeneratedAt);
        }
    }




}
