using Microsoft.Extensions.DependencyInjection;
using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class DashboardApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static List<string?> Codes(ApiResponse dashboard)
            => [.. Enumerable.Range(0, dashboard.Prop("widgets").GetArrayLength()).Select(i => dashboard.Prop("widgets")[i].GetProperty("code").GetString())];

        private static decimal Metric(ApiResponse dashboard, string widget, string key)
        {
            var found = Enumerable.Range(0, dashboard.Prop("widgets").GetArrayLength()).Select(i => dashboard.Prop("widgets")[i]).Single(w => w.GetProperty("code").GetString() == widget);

            return Enumerable.Range(0, found.GetProperty("metrics").GetArrayLength()).Select(i => found.GetProperty("metrics")[i]).Single(m => m.GetProperty("key").GetString() == key).GetProperty("value").GetDecimal();
        }

        [Fact]
        public async Task Without_reports_read_there_is_no_dashboard()
        {
            var user = await _api.CreateUserAsync("dashstudent", RoleNames.Student);
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            Assert.Equal(403, (await Client.GetAsync("/api/v1/dashboard", token)).Code);
            Assert.Equal(401, (await Client.GetAsync("/api/v1/dashboard", null)).Code);
        }

        [Fact]
        public async Task The_admin_sees_every_widget_with_its_numbers()
        {
            var admin = await _api.AdminTokenAsync();

            var dashboard = await Client.GetAsync("/api/v1/dashboard", admin);

            Assert.Equal(200, dashboard.Code);
            foreach (var code in new[] { "finance", "requests", "documents", "security", "notifications" })
            {
                Assert.Contains(code, Codes(dashboard));
            }

            Assert.Equal(0, dashboard.Prop("unavailable").GetArrayLength());
            Assert.True(Metric(dashboard, "finance", "openInvoices") >= 0);
            Assert.True(Metric(dashboard, "security", "failedLogins24h") >= 0);
        }

        [Fact]
        public async Task A_user_sees_only_the_widgets_their_permissions_allow()
        {
            var token = await RoleUsers.TokenAsync(_api, "dashdocs", [Permissions.Reports.Read, Permissions.Documents.Manage]);

            var dashboard = await Client.GetAsync("/api/v1/dashboard", token);

            Assert.Equal(200, dashboard.Code);
            Assert.Equal(["documents"], Codes(dashboard));
        }

        [Fact]
        public async Task The_requests_widget_counts_only_the_users_scope()
        {
            var department = Guid.NewGuid();
            var other = Guid.NewGuid();
            var studentUser = await _api.CreateUserAsync("dashreq", RoleNames.Student);
            var student = await _api.LoginTokenAsync(studentUser.UserName, studentUser.Password);

            foreach (var scope in new[] { department, department, department, other })
            {
                var started = await Client.PostAsync(
                    "/api/v1/workflows",
                    new { definitionCode = "WITHDRAWAL_REQUEST", referenceType = "TestRequest", referenceId = Guid.NewGuid(), scopeType = "Department", scopeId = scope },
                    student,
                    TestApi.NewIdempotencyKey());
                Assert.Equal(201, started.Code);
            }

            var head = await RoleUsers.TokenAsync(_api, "dashhead", [Permissions.Reports.Read, Permissions.Requests.Read], ScopeType.Department, department);

            var dashboard = await Client.GetAsync("/api/v1/dashboard", head);

            Assert.Equal(200, dashboard.Code);
            Assert.Equal(["requests"], Codes(dashboard));
            Assert.Equal(3m, Metric(dashboard, "requests", "inProgress"));
        }

        [Fact]
        public async Task A_second_call_within_a_minute_comes_from_the_cache()
        {
            var token = await RoleUsers.TokenAsync(_api, "dashcache", [Permissions.Reports.Read, Permissions.Documents.Manage]);

            var first = await Client.GetAsync("/api/v1/dashboard", token);
            var second = await Client.GetAsync("/api/v1/dashboard", token);

            Assert.Equal(
                first.Prop("widgets")[0].GetProperty("generatedAt").GetDateTimeOffset(),
                second.Prop("widgets")[0].GetProperty("generatedAt").GetDateTimeOffset());
        }
    }


}
