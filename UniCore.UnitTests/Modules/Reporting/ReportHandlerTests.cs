using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Reporting
{
    public class ReportHandlerTests
    {
        private static readonly Dictionary<string, string?> NoFilters = [];

        private static string[] Perms(params string[] extra) => [Permissions.Reports.Read, .. extra];

        // A user who can read reports and run "OPEN_REPORT" (needs Finance.Read) but not "AUDIT_REPORT".
        private static (ReportingEnv Env, StubReport Open, StubReport Audit, StubExporter Csv, ReportHandler Handler) Setup(IEnumerable<string> permissions, int rows = 5, int csvMax = 1000)
        {
            var env = new ReportingEnv().Grant(permissions);
            var open = new StubReport("OPEN_REPORT", Permissions.Finance.Read, rows);
            var audit = new StubReport("AUDIT_REPORT", Permissions.Audit.Read, rows, sensitive: true);
            var csv = new StubExporter(ReportFormat.Csv, csvMax);

            return (env, open, audit, csv, env.Handler([open, audit], csv, new StubExporter(ReportFormat.Pdf, 3)));
        }

        [Fact]
        public async Task The_list_shows_only_the_reports_the_user_may_run()
        {
            var (_, _, _, _, handler) = Setup(Perms(Permissions.Finance.Read));

            var listed = await handler.ListAsync();

            Assert.True(listed.TryGetValue(out var reports));
            Assert.Single(reports!);
            Assert.Equal("OPEN_REPORT", reports![0].Code);
        }

        [Fact]
        public async Task Without_reports_read_nothing_works_even_with_the_reports_own_permission()
        {
            var (env, _, _, _, handler) = Setup([Permissions.Finance.Read]);

            Assert.Equal(ErrorType.Forbidden, (await handler.ListAsync()).Errors[0].ErrorType);
            Assert.Equal(ErrorType.Forbidden, (await handler.DescribeAsync("OPEN_REPORT")).Errors[0].ErrorType);
            Assert.Equal(ErrorType.Forbidden, (await handler.RunAsync("OPEN_REPORT", NoFilters, new PaginationQuery(), env.Request)).Errors[0].ErrorType);
            Assert.Equal(ErrorType.Forbidden, (await handler.ExportAsync("OPEN_REPORT", "csv", NoFilters, env.Request)).Errors[0].ErrorType);
        }

        [Fact]
        public async Task A_grant_in_one_scope_is_enough_to_open_a_report()
        {
            var env = new ReportingEnv().Grant([Permissions.Reports.Read, Permissions.Requests.Read], new ScopeGrant(ScopeType.Department, Guid.NewGuid()));
            var handler = env.Handler([new StubReport("REQUESTS", Permissions.Requests.Read)], new StubExporter(ReportFormat.Csv));

            Assert.Single((await handler.ListAsync()).Value!);
            Assert.True((await handler.RunAsync("REQUESTS", NoFilters, new PaginationQuery(), env.Request)).IsSuccess);
        }

        [Fact]
        public async Task Nobody_signed_in_is_unauthorized()
        {
            var env = new ReportingEnv();
            env.User.UserId = null;
            var handler = env.Handler([new StubReport("OPEN_REPORT", Permissions.Finance.Read)], new StubExporter(ReportFormat.Csv));

            Assert.Equal(ErrorType.Unauthorized, (await handler.ListAsync()).Errors[0].ErrorType);
            Assert.Equal(ErrorType.Unauthorized, (await handler.RunAsync("OPEN_REPORT", NoFilters, new PaginationQuery(), env.Request)).Errors[0].ErrorType);
        }

        [Fact]
        public async Task A_report_the_user_has_no_permission_for_is_forbidden_and_an_unknown_code_is_not_found()
        {
            var (env, _, _, _, handler) = Setup(Perms(Permissions.Finance.Read));

            var denied = await handler.RunAsync("AUDIT_REPORT", NoFilters, new PaginationQuery(), env.Request);
            var unknown = await handler.RunAsync("NOPE", NoFilters, new PaginationQuery(), env.Request);

            Assert.Equal(ErrorType.Forbidden, denied.Errors[0].ErrorType);
            Assert.Equal("REPORT_NOT_FOUND", unknown.Errors[0].Code);
            Assert.Equal("REPORT_NOT_FOUND", (await handler.DescribeAsync("NOPE")).Errors[0].Code);
        }

        [Fact]
        public async Task Describe_lists_the_parameters_and_columns_and_the_export_formats_only_for_those_who_may_export()
        {
            var withoutExport = Setup(Perms(Permissions.Finance.Read)).Handler;
            var withExport = Setup(Perms(Permissions.Finance.Read, Permissions.Reports.Export)).Handler;

            var plain = (await withoutExport.DescribeAsync("open_report")).Value!;
            var exportable = (await withExport.DescribeAsync("OPEN_REPORT")).Value!;

            Assert.Equal(2, plain.Parameters.Count);
            Assert.Equal(2, plain.Columns.Count);
            Assert.False(plain.CanExport);
            Assert.Empty(plain.ExportFormats);
            Assert.True(exportable.CanExport);
            Assert.Equal(["csv", "pdf"], exportable.ExportFormats);
        }

        [Fact]
        public async Task Run_returns_the_asked_page_and_the_total()
        {
            var (env, open, _, _, handler) = Setup(Perms(Permissions.Finance.Read), rows: 5);

            var ran = await handler.RunAsync("OPEN_REPORT", NoFilters, new PaginationQuery(2, 2), env.Request);

            Assert.True(ran.TryGetValue(out var result));
            Assert.Equal(2, result!.Rows.Count);
            Assert.Equal(3, result.Rows[0][0]);
            Assert.Equal(5, result.TotalRows);
            Assert.Equal(2, result.Page);
            Assert.Equal(2, result.PageSize);
            Assert.Equal(ReportingEnv.Now, result.GeneratedAt);
            Assert.Equal(new ReportPage(2, 2), open.LastPage);
        }

        [Fact]
        public async Task Run_passes_the_filters_as_typed_arguments_and_refuses_bad_ones_without_running_the_report()
        {
            var (env, open, _, _, handler) = Setup(Perms(Permissions.Finance.Read));

            var good = await handler.RunAsync("OPEN_REPORT", new Dictionary<string, string?> { ["from"] = "2026-05-01", ["status"] = "closed" }, new PaginationQuery(), env.Request);
            var calls = open.LastArguments;
            var (env2, open2, _, _, handler2) = Setup(Perms(Permissions.Finance.Read));
            var bad = await handler2.RunAsync("OPEN_REPORT", new Dictionary<string, string?> { ["from"] = "yesterday" }, new PaginationQuery(), env2.Request);

            Assert.True(good.IsSuccess);
            Assert.Equal(new DateOnly(2026, 5, 1), calls!.Date("from"));
            Assert.Equal("Closed", calls.Text("status"));
            Assert.Equal("REPORT_PARAMETER_INVALID", bad.Errors[0].Code);
            Assert.Null(open2.LastArguments);
        }

        [Fact]
        public async Task A_report_failure_is_returned_and_not_audited()
        {
            var (env, _, audit, _, handler) = Setup(Perms(Permissions.Audit.Read));
            audit.Fail = UniCore.Domain.Modules.Reporting.ReportingErrors.DataUnavailable;

            var ran = await handler.RunAsync("AUDIT_REPORT", NoFilters, new PaginationQuery(), env.Request);

            Assert.Equal("REPORT_DATA_UNAVAILABLE", ran.Errors[0].Code);
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task Running_a_sensitive_report_is_audited_with_its_filters_and_a_normal_one_is_not()
        {
            var (env, _, _, _, handler) = Setup(Perms(Permissions.Finance.Read, Permissions.Audit.Read));

            await handler.RunAsync("OPEN_REPORT", NoFilters, new PaginationQuery(), env.Request);
            Assert.Empty(env.Audit.Entries);

            await handler.RunAsync("AUDIT_REPORT", new Dictionary<string, string?> { ["from"] = "2026-05-01" }, new PaginationQuery(), env.Request);

            var entry = Assert.Single(env.Audit.Entries);
            Assert.Equal(AuditActions.ReportViewed, entry.Action);
            Assert.Equal("Report", entry.EntityName);
            Assert.Equal("AUDIT_REPORT", entry.EntityId);
            Assert.Equal("from=2026-05-01", entry.NewValues);
            Assert.Equal("10.0.0.1", entry.IpAddress);
        }

        [Fact]
        public async Task Export_needs_the_export_permission_too()
        {
            var (env, _, _, _, handler) = Setup(Perms(Permissions.Finance.Read));

            var exported = await handler.ExportAsync("OPEN_REPORT", "csv", NoFilters, env.Request);

            Assert.Equal(ErrorType.Forbidden, exported.Errors[0].ErrorType);
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task Export_makes_a_named_file_in_the_asked_format_and_audits_it()
        {
            var (env, open, _, csv, handler) = Setup(Perms(Permissions.Finance.Read, Permissions.Reports.Export));

            var exported = await handler.ExportAsync("OPEN_REPORT", " CSV ", new Dictionary<string, string?> { ["status"] = "open" }, env.Request);

            Assert.True(exported.TryGetValue(out var file));
            Assert.Equal("open-report-20261108-1030.csv", file!.FileName);
            Assert.Equal("application/x-csv", file.ContentType);
            Assert.Equal(new byte[] { 1, 2, 3 }, file.Content);
            Assert.Equal(new ReportPage(0, 1001), open.LastPage);
            Assert.Equal("Report OPEN_REPORT", csv.LastModel!.Title);
            Assert.Equal("status=Open", csv.LastModel.Parameters);
            Assert.Equal(5, csv.LastModel.Table.Rows.Count);
            Assert.Equal(ReportingEnv.Now, csv.LastModel.GeneratedAt);

            var entry = Assert.Single(env.Audit.Entries);
            Assert.Equal(AuditActions.ReportExported, entry.Action);
            Assert.Equal("OPEN_REPORT", entry.EntityId);
            Assert.Equal("format=csv; rows=5; status=Open", entry.NewValues);
        }

        [Fact]
        public async Task Export_without_filters_says_so_in_the_file_and_the_audit_has_no_dangling_separator()
        {
            var (env, _, _, csv, handler) = Setup(Perms(Permissions.Finance.Read, Permissions.Reports.Export));

            await handler.ExportAsync("OPEN_REPORT", "csv", NoFilters, env.Request);

            Assert.Equal("No filters", csv.LastModel!.Parameters);
            Assert.Equal("format=csv; rows=5", env.Audit.Entries[0].NewValues);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("docx")]
        [InlineData("xlsx")]
        [InlineData("0")]
        public async Task An_unknown_or_unavailable_format_is_refused(string? format)
        {
            var (env, _, _, _, handler) = Setup(Perms(Permissions.Finance.Read, Permissions.Reports.Export));

            var exported = await handler.ExportAsync("OPEN_REPORT", format, NoFilters, env.Request);

            Assert.Equal("REPORT_FORMAT_UNSUPPORTED", exported.Errors[0].Code);
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task A_report_bigger_than_the_format_allows_is_refused_without_building_the_file()
        {
            var (env, _, _, csv, handler) = Setup(Perms(Permissions.Finance.Read, Permissions.Reports.Export), rows: 10, csvMax: 9);

            var exported = await handler.ExportAsync("OPEN_REPORT", "csv", NoFilters, env.Request);

            Assert.Equal("REPORT_EXPORT_TOO_LARGE", exported.Errors[0].Code);
            Assert.Contains("9", exported.Errors[0].Description);
            Assert.Contains("CSV", exported.Errors[0].Description);
            Assert.Null(csv.LastModel);
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task A_report_exactly_at_the_limit_is_exported()
        {
            var (env, _, _, _, handler) = Setup(Perms(Permissions.Finance.Read, Permissions.Reports.Export), rows: 9, csvMax: 9);

            Assert.True((await handler.ExportAsync("OPEN_REPORT", "csv", NoFilters, env.Request)).IsSuccess);
        }

        [Fact]
        public async Task The_pdf_has_its_own_smaller_limit()
        {
            var (env, _, _, _, handler) = Setup(Perms(Permissions.Finance.Read, Permissions.Reports.Export), rows: 5);

            var pdf = await handler.ExportAsync("OPEN_REPORT", "pdf", NoFilters, env.Request);

            Assert.Equal("REPORT_EXPORT_TOO_LARGE", pdf.Errors[0].Code);
        }

        [Fact]
        public async Task Exporting_a_report_you_may_not_run_is_forbidden_even_with_the_export_permission()
        {
            var (env, _, _, _, handler) = Setup(Perms(Permissions.Reports.Export));

            var exported = await handler.ExportAsync("OPEN_REPORT", "csv", NoFilters, env.Request);

            Assert.Equal(ErrorType.Forbidden, exported.Errors[0].ErrorType);
        }
    }




}
