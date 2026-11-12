using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Text.Json;
using UniCore.Application.Modules.Reporting.Reports;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class ReportsApiTests(ApiFixture fixture)
    {
        private static readonly string[] AllReports =
        [
            FinanceInvoiceSummaryReport.Code,
            FinanceOutstandingBalancesReport.Code,
            FinanceCollectionsReport.Code,
            WorkflowRequestsReport.Code,
            DocumentsByTypeReport.Code,
            NotificationDeliveryReport.Code,
            AuditActivityReport.Code
        ];

        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string Today(int offsetDays = 0) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offsetDays).ToString("yyyy-MM-dd");

        private static string Range => $"from={Today(-1)}&to={Today(1)}";

        private async Task<string> StudentTokenAsync()
        {
            var user = await _api.CreateUserAsync("repstudent", RoleNames.Student);

            return await _api.LoginTokenAsync(user.UserName, user.Password);
        }

        private static JsonElement LastRow(ApiResponse run)
        {
            var rows = run.Prop("rows");

            return rows[rows.GetArrayLength() - 1];
        }

        private async Task<(HttpResponseMessage Response, byte[] Content)> GetFileAsync(string token, string path)
        {
            using var client = fixture.CreateHttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(request);

            return (response, await response.Content.ReadAsByteArrayAsync());
        }

        // ---------- who may do what ----------

        [Fact]
        public async Task Without_reports_read_every_report_endpoint_is_refused()
        {
            var student = await StudentTokenAsync();

            Assert.Equal(403, (await Client.GetAsync("/api/v1/reports", student)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/reports/{AuditActivityReport.Code}", student)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/reports/{AuditActivityReport.Code}/run", student)).Code);
            Assert.Equal(403, (await Client.GetAsync($"/api/v1/reports/{AuditActivityReport.Code}/export?format=csv", student)).Code);
            Assert.Equal(401, (await Client.GetAsync("/api/v1/reports", null)).Code);
        }

        [Fact]
        public async Task The_catalog_lists_only_the_reports_the_users_permissions_allow()
        {
            var admin = await _api.AdminTokenAsync();
            var documents = await RoleUsers.TokenAsync(_api, "repdocs", [Permissions.Reports.Read, Permissions.Documents.Manage]);

            var all = await Client.GetAsync("/api/v1/reports", admin);
            var some = await Client.GetAsync("/api/v1/reports", documents);

            Assert.Equal(200, all.Code);
            var allCodes = Enumerable.Range(0, all.Body.GetArrayLength()).Select(i => all.Body[i].GetProperty("code").GetString()).ToList();
            foreach (var code in AllReports)
            {
                Assert.Contains(code, allCodes);
            }

            Assert.Equal(200, some.Code);
            Assert.Equal([DocumentsByTypeReport.Code], Enumerable.Range(0, some.Body.GetArrayLength()).Select(i => some.Body[i].GetProperty("code").GetString()));
        }

        [Fact]
        public async Task A_report_the_user_has_no_permission_for_is_forbidden_and_an_unknown_one_is_not_found()
        {
            var documents = await RoleUsers.TokenAsync(_api, "repdocs", [Permissions.Reports.Read, Permissions.Documents.Manage]);

            Assert.Equal(403, (await Client.GetAsync($"/api/v1/reports/{FinanceInvoiceSummaryReport.Code}/run", documents)).Code);
            Assert.Equal(404, (await Client.GetAsync("/api/v1/reports/NO_SUCH_REPORT", documents)).Code);
            Assert.Equal("REPORT_NOT_FOUND", (await Client.GetAsync("/api/v1/reports/NO_SUCH_REPORT/run", documents)).ErrorCode);
        }

        [Fact]
        public async Task Describe_returns_the_filters_the_columns_and_the_export_formats()
        {
            var admin = await _api.AdminTokenAsync();

            var described = await Client.GetAsync($"/api/v1/reports/{FinanceCollectionsReport.Code.ToLowerInvariant()}", admin);

            Assert.Equal(200, described.Code);
            Assert.Equal(FinanceCollectionsReport.Code, described.Str("code"));
            Assert.Equal(6, described.Prop("columns").GetArrayLength());
            Assert.True(described.Prop("canExport").GetBoolean());
            Assert.Equal(["csv", "pdf", "xlsx"], Enumerable.Range(0, described.Prop("exportFormats").GetArrayLength()).Select(i => described.Prop("exportFormats")[i].GetString()).Order());

            var from = Enumerable.Range(0, described.Prop("parameters").GetArrayLength()).Select(i => described.Prop("parameters")[i]).Single(p => p.GetProperty("name").GetString() == "from");
            Assert.True(from.GetProperty("required").GetBoolean());
        }

        // ---------- running ----------

        [Fact]
        public async Task Bad_filters_are_refused_with_a_clear_code()
        {
            var admin = await _api.AdminTokenAsync();
            var path = $"/api/v1/reports/{FinanceCollectionsReport.Code}/run";

            Assert.Equal("REPORT_PARAMETER_REQUIRED", (await Client.GetAsync(path, admin)).ErrorCode);
            Assert.Equal("REPORT_PARAMETER_INVALID", (await Client.GetAsync($"{path}?from=yesterday&to=today", admin)).ErrorCode);
            Assert.Equal("REPORT_PARAMETER_UNKNOWN", (await Client.GetAsync($"{path}?{Range}&colour=red", admin)).ErrorCode);
            Assert.Equal("REPORT_DATE_RANGE_INVALID", (await Client.GetAsync($"{path}?from=2026-05-02&to=2026-05-01", admin)).ErrorCode);

            var wide = await Client.GetAsync($"{path}?from=2020-01-01&to=2026-01-01", admin);
            Assert.Equal(422, wide.Code);
            Assert.Equal("REPORT_DATE_RANGE_TOO_WIDE", wide.ErrorCode);
        }

        [Fact]
        public async Task The_invoice_summary_counts_a_real_invoice()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 1000m);
            var paid = await Client.PostAsync($"/api/v1/invoices/{invoice.InvoiceId}/payments", new { amount = 400m, method = "Cash" }, admin, TestApi.NewIdempotencyKey());
            Assert.Equal(201, paid.Code);

            var run = await Client.GetAsync($"/api/v1/reports/{FinanceInvoiceSummaryReport.Code}/run?{Range}", admin);

            Assert.Equal(200, run.Code);
            var statuses = Enumerable.Range(0, run.Prop("rows").GetArrayLength()).Select(i => run.Prop("rows")[i][0].GetString()).ToList();
            Assert.Contains("PartiallyPaid", statuses);

            var total = LastRow(run);
            Assert.Equal("Total", total[0].GetString());
            Assert.True(total[1].GetInt32() >= 1);
            Assert.True(total[2].GetDecimal() >= 1000m);
            Assert.True(total[3].GetDecimal() >= 400m);
            Assert.Equal(DateTime.UtcNow.Year, run.Prop("generatedAt").GetDateTimeOffset().Year);
        }

        [Fact]
        public async Task The_status_filter_keeps_only_that_status()
        {
            var admin = await _api.AdminTokenAsync();
            await InvoiceScenario.IssueAsync(_api, admin, 250m);

            var run = await Client.GetAsync($"/api/v1/reports/{FinanceInvoiceSummaryReport.Code}/run?{Range}&status=issued", admin);

            Assert.Equal(200, run.Code);
            var rows = run.Prop("rows");
            Assert.Equal("Issued", rows[0][0].GetString());
            Assert.Equal("Total", rows[rows.GetArrayLength() - 1][0].GetString());
            Assert.Equal(2, rows.GetArrayLength());
        }

        [Fact]
        public async Task Outstanding_balances_list_the_unpaid_invoice_and_every_run_is_audited()
        {
            var admin = await _api.AdminTokenAsync();
            await InvoiceScenario.IssueAsync(_api, admin, 1_234_567m);

            var run = await Client.GetAsync($"/api/v1/reports/{FinanceOutstandingBalancesReport.Code}/run?pageSize=100", admin);

            Assert.Equal(200, run.Code);
            var balances = Enumerable.Range(0, run.Prop("rows").GetArrayLength()).Select(i => run.Prop("rows")[i]).ToList();
            Assert.Contains(balances, row => row[2].GetDecimal() == 1_234_567m && row[1].GetInt32() == 1);

            var audit = await Client.GetAsync($"/api/v1/audit-logs?action={AuditActions.ReportViewed}&entityName=Report&entityId={FinanceOutstandingBalancesReport.Code}", admin);

            Assert.Equal(200, audit.Code);
            Assert.True(audit.Prop("totalCount").GetInt32() >= 1);
        }

        [Fact]
        public async Task Collections_include_a_payment_made_today()
        {
            var admin = await _api.AdminTokenAsync();
            var invoice = await InvoiceScenario.IssueAsync(_api, admin, 500m);
            Assert.Equal(201, (await Client.PostAsync($"/api/v1/invoices/{invoice.InvoiceId}/payments", new { amount = 500m, method = "Cash" }, admin, TestApi.NewIdempotencyKey())).Code);

            var run = await Client.GetAsync($"/api/v1/reports/{FinanceCollectionsReport.Code}/run?{Range}&method=cash", admin);

            Assert.Equal(200, run.Code);
            var total = LastRow(run);
            Assert.Equal("Total", total[1].GetString());
            Assert.True(total[2].GetInt32() >= 1);
            Assert.True(total[3].GetDecimal() >= 500m);
        }

        [Fact]
        public async Task A_run_returns_the_asked_page_and_the_total()
        {
            var admin = await _api.AdminTokenAsync();
            await _api.CreateUserAsync("reppaging");

            var run = await Client.GetAsync($"/api/v1/reports/{AuditActivityReport.Code}/run?page=1&pageSize=1", admin);

            Assert.Equal(200, run.Code);
            Assert.Equal(1, run.Prop("page").GetInt32());
            Assert.Equal(1, run.Prop("pageSize").GetInt32());
            Assert.Equal(1, run.Prop("rows").GetArrayLength());
            Assert.True(run.Prop("totalRows").GetInt32() >= 1);
        }

        [Fact]
        public async Task A_scoped_user_sees_only_the_requests_of_their_scope()
        {
            var admin = await _api.AdminTokenAsync();
            var departmentA = Guid.NewGuid();
            var departmentB = Guid.NewGuid();
            var student = await StudentTokenAsync();

            async Task StartAsync(Guid department)
            {
                var started = await Client.PostAsync(
                    "/api/v1/workflows",
                    new { definitionCode = "WITHDRAWAL_REQUEST", referenceType = "TestRequest", referenceId = Guid.NewGuid(), scopeType = "Department", scopeId = department },
                    student,
                    TestApi.NewIdempotencyKey());

                Assert.Equal(201, started.Code);
            }

            await StartAsync(departmentA);
            await StartAsync(departmentA);
            await StartAsync(departmentB);

            var headA = await RoleUsers.TokenAsync(_api, "reploghA", [Permissions.Reports.Read, Permissions.Requests.Read], ScopeType.Department, departmentA);
            var headB = await RoleUsers.TokenAsync(_api, "reploghB", [Permissions.Reports.Read, Permissions.Requests.Read], ScopeType.Department, departmentB);
            var path = $"/api/v1/reports/{WorkflowRequestsReport.Code}/run?pageSize=100";

            var seenByA = await Client.GetAsync(path, headA);
            var seenByB = await Client.GetAsync(path, headB);
            var seenByAdmin = await Client.GetAsync(path, admin);

            Assert.Equal(200, seenByA.Code);
            Assert.Equal(2, LastRow(seenByA)[2].GetInt32());
            Assert.Equal(1, LastRow(seenByB)[2].GetInt32());
            Assert.True(LastRow(seenByAdmin)[2].GetInt32() >= 3);
        }

        // ---------- exporting ----------

        [Fact]
        public async Task Export_needs_the_export_permission()
        {
            var without = await RoleUsers.TokenAsync(_api, "repnoexp", [Permissions.Reports.Read, Permissions.Documents.Manage]);
            var with = await RoleUsers.TokenAsync(_api, "repexp", [Permissions.Reports.Read, Permissions.Reports.Export, Permissions.Documents.Manage]);
            var path = $"/api/v1/reports/{DocumentsByTypeReport.Code}/export?format=csv";

            Assert.Equal(403, (await Client.GetAsync(path, without)).Code);
            Assert.True((await GetFileAsync(with, path)).Response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Export_downloads_a_csv_file_with_a_proper_name_and_safe_headers()
        {
            var admin = await _api.AdminTokenAsync();

            var (response, content) = await GetFileAsync(admin, $"/api/v1/reports/{DocumentsByTypeReport.Code}/export?format=CSV");

            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            var name = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;
            Assert.StartsWith("documents-by-type-", name!.Trim('"'));
            Assert.EndsWith(".csv", name.Trim('"'));
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, content[..3]);
            Assert.StartsWith("Type code,Type,Sensitive,Active,Deleted,Active size (MB)", System.Text.Encoding.UTF8.GetString(content).TrimStart('\uFEFF'));

            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Contains("no-store", string.Join(",", response.Headers.GetValues("Cache-Control")));
        }

        [Fact]
        public async Task Export_makes_real_excel_and_pdf_files()
        {
            var admin = await _api.AdminTokenAsync();
            var path = $"/api/v1/reports/{AuditActivityReport.Code}/export";

            var (xlsx, xlsxBytes) = await GetFileAsync(admin, $"{path}?format=xlsx");
            var (pdf, pdfBytes) = await GetFileAsync(admin, $"{path}?format=pdf");

            Assert.Equal(200, (int)xlsx.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType?.MediaType);
            Assert.Equal(new byte[] { (byte)'P', (byte)'K' }, xlsxBytes[..2]);

            Assert.Equal(200, (int)pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
            Assert.StartsWith("%PDF-1.4", System.Text.Encoding.Latin1.GetString(pdfBytes));
        }

        [Theory]
        [InlineData("docx")]
        [InlineData("")]
        public async Task An_unknown_format_is_refused(string format)
        {
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync($"/api/v1/reports/{DocumentsByTypeReport.Code}/export?format={format}", admin);

            Assert.Equal(400, response.Code);
            Assert.Equal("REPORT_FORMAT_UNSUPPORTED", response.ErrorCode);
        }

        [Fact]
        public async Task Export_takes_the_same_filters_as_a_run_and_refuses_bad_ones()
        {
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync($"/api/v1/reports/{FinanceCollectionsReport.Code}/export?format=csv&from=nonsense&to=2026-01-01", admin);

            Assert.Equal(400, response.Code);
            Assert.Equal("REPORT_PARAMETER_INVALID", response.ErrorCode);
        }

        [Fact]
        public async Task Every_export_is_audited_with_its_format_row_count_and_filters()
        {
            var admin = await _api.AdminTokenAsync();
            var from = Today(-3);
            var (response, _) = await GetFileAsync(admin, $"/api/v1/reports/{FinanceCollectionsReport.Code}/export?format=csv&from={from}&to={Today()}");
            Assert.Equal(200, (int)response.StatusCode);

            var audit = await Client.GetAsync($"/api/v1/audit-logs?action={AuditActions.ReportExported}&entityName=Report&entityId={FinanceCollectionsReport.Code}&pageSize=50", admin);

            Assert.Equal(200, audit.Code);
            var details = Enumerable.Range(0, audit.Prop("items").GetArrayLength())
                .Select(i => audit.Prop("items")[i])
                .Select(item => item.TryGetProperty("newValues", out var values) ? values.GetString() : null)
                .ToList();
            Assert.Contains(details, value => value is not null && value.Contains("format=csv") && value.Contains($"from={from}"));
        }
    }


}
