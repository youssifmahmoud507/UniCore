using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Reports;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Notifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.UnitTests.Modules.Reporting
{
    public class PlatformReportsTests
    {
        private static readonly DateTimeOffset Now = ReportingEnv.Now;
        private static readonly Guid Faculty = Guid.NewGuid();
        private static readonly Guid Department = Guid.NewGuid();
        private static readonly Guid OtherDepartment = Guid.NewGuid();

        private static WorkflowFact Request(string code, WorkflowInstanceStatus status, Guid? scopeId, int startedDaysAgo, int? durationDays = null)
            => new(code, status, scopeId is null ? null : ScopeType.Department, scopeId, Now.AddDays(-startedDaysAgo), durationDays is null ? null : Now.AddDays(-startedDaysAgo + durationDays.Value));

        [Fact]
        public void Requests_are_counted_by_type_and_status_with_average_days()
        {
            var rows = WorkflowRequestsReport.Build(
            [
                Request("WITHDRAWAL", WorkflowInstanceStatus.Completed, Department, 20, 4),
                Request("WITHDRAWAL", WorkflowInstanceStatus.Completed, Department, 20, 6),
                Request("WITHDRAWAL", WorkflowInstanceStatus.InProgress, Department, 3),
                Request("COMPLAINT", WorkflowInstanceStatus.Rejected, Department, 10, 2)
            ], ReportScope.Everything, Now, null);

            Assert.Equal(4, rows.Count);
            Assert.Equal(new object?[] { "COMPLAINT", "Rejected", 1, 2m }, rows[0].ToArray());
            Assert.Equal(new object?[] { "WITHDRAWAL", "InProgress", 1, 3m }, rows[1].ToArray());
            Assert.Equal(new object?[] { "WITHDRAWAL", "Completed", 2, 5m }, rows[2].ToArray());
            Assert.Equal(new object?[] { "Total", null, 4, null }, rows[3].ToArray());
        }

        [Fact]
        public void A_user_sees_only_the_requests_of_their_scope()
        {
            var scope = new ReportScope(false, new HashSet<Guid> { Department });

            var rows = WorkflowRequestsReport.Build(
            [
                Request("WITHDRAWAL", WorkflowInstanceStatus.InProgress, Department, 1),
                Request("WITHDRAWAL", WorkflowInstanceStatus.InProgress, OtherDepartment, 1),
                Request("WITHDRAWAL", WorkflowInstanceStatus.InProgress, null, 1)
            ], scope, Now, null);

            Assert.Equal(2, rows.Count);
            Assert.Equal(1, rows[0][2]);
            Assert.Equal(1, rows[1][2]);
        }

        [Fact]
        public void Requests_can_be_limited_to_one_type_in_any_letter_case()
        {
            var rows = WorkflowRequestsReport.Build([Request("A", WorkflowInstanceStatus.InProgress, null, 1), Request("B", WorkflowInstanceStatus.InProgress, null, 1)], ReportScope.Everything, Now, "b");

            Assert.Equal("B", rows[0][0]);
        }

        [Fact]
        public async Task The_requests_report_asks_the_data_source_for_the_dates_and_applies_the_users_scope()
        {
            var env = new ReportingEnv().Grant([Permissions.Requests.Read], new ScopeGrant(ScopeType.Department, Department));
            env.Data.Workflows.Add(Request("WITHDRAWAL", WorkflowInstanceStatus.InProgress, Department, 1));
            env.Data.Workflows.Add(Request("WITHDRAWAL", WorkflowInstanceStatus.InProgress, OtherDepartment, 1));
            var report = new WorkflowRequestsReport(env.Data, new ReportScopeResolver(env.Access, env.Hierarchy), env.Clock);
            var arguments = new ReportArguments(new Dictionary<string, object> { ["from"] = new DateOnly(2026, 11, 1), ["to"] = new DateOnly(2026, 11, 7) });

            var ran = await report.RunAsync(arguments, ReportPage.First(50));

            Assert.True(ran.TryGetValue(out var table));
            Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), env.Data.WorkflowRange.From);
            Assert.Equal(new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero), env.Data.WorkflowRange.ToExclusive);
            Assert.Equal(2, table!.TotalRows);
        }

        [Fact]
        public void Documents_are_counted_per_type_with_the_size_of_the_active_ones()
        {
            var rows = DocumentsByTypeReport.Build(
            [
                new DocumentStat("TRANSCRIPT", "Transcript", true, DocumentStatus.Active, 4, 2_097_152),
                new DocumentStat("TRANSCRIPT", "Transcript", true, DocumentStatus.Deleted, 1, 1_048_576),
                new DocumentStat("CERTIFICATE", "Certificate", false, DocumentStatus.Active, 10, 524_288)
            ]);

            Assert.Equal(2, rows.Count);
            Assert.Equal(new object?[] { "CERTIFICATE", "Certificate", false, 10, 0, 0.5m }, rows[0].ToArray());
            Assert.Equal(new object?[] { "TRANSCRIPT", "Transcript", true, 4, 1, 2m }, rows[1].ToArray());
        }

        [Fact]
        public void Deliveries_show_the_share_of_each_status_inside_its_channel()
        {
            var rows = NotificationDeliveryReport.Build(
            [
                new DeliveryStat(NotificationChannel.Email, NotificationDeliveryStatus.Sent, 90),
                new DeliveryStat(NotificationChannel.Email, NotificationDeliveryStatus.Failed, 10),
                new DeliveryStat(NotificationChannel.InApp, NotificationDeliveryStatus.Sent, 5)
            ]);

            Assert.Equal(3, rows.Count);
            Assert.Equal(new object?[] { "InApp", "Sent", 5, 100m }, rows[0].ToArray());
            Assert.Equal("Email", rows[1][0]);
            Assert.Equal(90m, rows[1][3]);
            Assert.Equal(10m, rows[2][3]);
        }

        [Fact]
        public async Task Reports_without_dates_cover_the_last_seven_days_ending_today()
        {
            var data = new FakeReportData();
            var clock = new UniCore.UnitTests.Common.FakeClock(Now);

            await new NotificationDeliveryReport(data, clock).RunAsync(ReportArguments.None, ReportPage.First(10));
            await new AuditActivityReport(data, clock).RunAsync(ReportArguments.None, ReportPage.First(10));

            Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero), data.DeliveryRange.From);
            Assert.Equal(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero), data.DeliveryRange.ToExclusive);
            Assert.Equal(data.DeliveryRange, data.AuditRange);
        }

        [Fact]
        public async Task The_date_range_of_these_reports_is_limited_to_90_days()
        {
            var data = new FakeReportData();
            var clock = new UniCore.UnitTests.Common.FakeClock(Now);
            var wide = new ReportArguments(new Dictionary<string, object> { ["from"] = new DateOnly(2026, 1, 1), ["to"] = new DateOnly(2026, 11, 1) });

            var audit = await new AuditActivityReport(data, clock).RunAsync(wide, ReportPage.First(10));
            var deliveries = await new NotificationDeliveryReport(data, clock).RunAsync(wide, ReportPage.First(10));

            Assert.Equal("REPORT_DATE_RANGE_TOO_WIDE", audit.Errors[0].Code);
            Assert.Equal("REPORT_DATE_RANGE_TOO_WIDE", deliveries.Errors[0].Code);
            Assert.Equal(0, data.Calls);
        }

        [Fact]
        public void Audit_actions_are_added_up_and_the_most_frequent_comes_first()
        {
            var rows = AuditActivityReport.Build(
            [
                new AuditStat("AUTH_LOGIN", 5, Now.AddHours(-3)),
                new AuditStat("REPORT_EXPORTED", 9, Now.AddHours(-1)),
                new AuditStat("AUTH_LOGIN", 7, Now.AddHours(-2)),
                new AuditStat("AAA_FIRST", 12, Now.AddHours(-9))
            ]);

            Assert.Equal(3, rows.Count);
            Assert.Equal(new object?[] { "AAA_FIRST", 12, Now.AddHours(-9) }, rows[0].ToArray());
            Assert.Equal(new object?[] { "AUTH_LOGIN", 12, Now.AddHours(-2) }, rows[1].ToArray());
            Assert.Equal("REPORT_EXPORTED", rows[2][0]);
        }
    }




}
