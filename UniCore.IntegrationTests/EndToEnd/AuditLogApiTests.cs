using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCore.Domain.Modules.Audit;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class AuditLogApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private async Task<List<AuditLog>> RowsAsync(string action)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await db.Set<AuditLog>().AsNoTracking().Where(a => a.Action == action).ToListAsync();
        }

        [Fact]
        public async Task Creating_a_user_writes_an_audit_row_with_actor_entity_and_correlation_id()
        {
            var user = await _api.CreateUserAsync("auditcreate", setPassword: false);

            var rows = await RowsAsync(AuditActions.UserCreated);

            var row = Assert.Single(rows, r => r.EntityId == user.Id.ToString());
            Assert.NotNull(row.ActorUserId);
            Assert.Equal("User", row.EntityName);
            Assert.False(string.IsNullOrEmpty(row.CorrelationId));
            Assert.True(row.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        }

        [Fact]
        public async Task A_failed_login_is_audited_without_the_login_name_or_an_actor()
        {
            var user = await _api.CreateUserAsync("auditfail");
            var before = (await RowsAsync(AuditActions.AuthLoginFailed)).Count;

            var response = await _api.LoginAsync(user.UserName, "Wrong#Passw0rd1");

            Assert.Equal(401, response.Code);

            var after = await RowsAsync(AuditActions.AuthLoginFailed);
            Assert.Equal(before + 1, after.Count);

            var newest = after.MaxBy(r => r.CreatedAt);
            Assert.NotNull(newest);
            Assert.Null(newest.ActorUserId);
            Assert.Null(newest.EntityId);
            Assert.Null(newest.NewValues);
            Assert.False(string.IsNullOrEmpty(newest.CorrelationId));
        }
    }

    [Collection(ApiCollection.Name)]
    public sealed class AuditLogEndpointTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        [Fact]
        public async Task Anonymous_callers_are_rejected()
        {
            var response = await Client.GetAsync("/api/v1/audit-logs");

            Assert.Equal(401, response.Code);
        }

        [Fact]
        public async Task A_user_without_Audit_Read_gets_403()
        {
            var user = await _api.CreateUserAsync("auditnoperm");
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            var response = await Client.GetAsync("/api/v1/audit-logs", token);

            Assert.Equal(403, response.Code);
        }

        [Fact]
        public async Task Admin_can_filter_by_action_and_entity()
        {
            var user = await _api.CreateUserAsync("auditlist", setPassword: false);
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync(
                $"/api/v1/audit-logs?action={AuditActions.UserCreated}&entityName=User&entityId={user.Id}", admin);

            Assert.Equal(200, response.Code);
            Assert.Equal(1, response.Prop("totalCount").GetInt32());

            var item = response.Prop("items")[0];
            Assert.Equal(AuditActions.UserCreated, item.GetProperty("action").GetString());
            Assert.Equal(user.Id.ToString(), item.GetProperty("entityId").GetString());
        }

        [Fact]
        public async Task The_action_filter_ignores_case_and_surrounding_spaces()
        {
            var user = await _api.CreateUserAsync("auditcase", setPassword: false);
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync(
                $"/api/v1/audit-logs?action=%20user_created%20&entityId={user.Id}", admin);

            Assert.Equal(200, response.Code);
            Assert.Equal(1, response.Prop("totalCount").GetInt32());
        }

        [Fact]
        public async Task Paging_is_capped_and_newest_rows_come_first()
        {
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync("/api/v1/audit-logs?page=1&pageSize=1000", admin);

            Assert.Equal(200, response.Code);
            Assert.Equal(100, response.Prop("pageSize").GetInt32());

            var items = response.Prop("items");
            var dates = Enumerable.Range(0, items.GetArrayLength())
                .Select(i => items[i].GetProperty("createdAt").GetDateTimeOffset())
                .ToList();

            Assert.Equal(dates.OrderByDescending(d => d), dates);
        }

        [Fact]
        public async Task A_start_after_the_end_is_a_validation_error()
        {
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync(
                "/api/v1/audit-logs?from=2026-12-31T00:00:00Z&to=2026-01-01T00:00:00Z", admin);

            Assert.Equal(400, response.Code);
            Assert.Equal("AUDIT_FILTER_INVALID", response.ErrorCode);
        }

        [Fact]
        public async Task Get_by_id_returns_the_row_and_an_unknown_id_is_404()
        {
            var user = await _api.CreateUserAsync("auditget", setPassword: false);
            var admin = await _api.AdminTokenAsync();

            var list = await Client.GetAsync($"/api/v1/audit-logs?entityId={user.Id}&action={AuditActions.UserCreated}", admin);
            var id = list.Prop("items")[0].GetProperty("id").GetString();

            var found = await Client.GetAsync($"/api/v1/audit-logs/{id}", admin);
            var missing = await Client.GetAsync($"/api/v1/audit-logs/{Guid.NewGuid()}", admin);

            Assert.Equal(200, found.Code);
            Assert.Equal(404, missing.Code);
            Assert.Equal("AUDIT_LOG_NOT_FOUND", missing.ErrorCode);
        }

        [Fact]
        public async Task The_audit_trail_cannot_be_written_through_the_api()
        {
            var admin = await _api.AdminTokenAsync();

            var post = await Client.PostAsync("/api/v1/audit-logs", new { action = "X" }, admin);
            var delete = await Client.DeleteAsync($"/api/v1/audit-logs/{Guid.NewGuid()}", admin);

            Assert.Contains(post.Code, new[] { 404, 405 });
            Assert.Contains(delete.Code, new[] { 404, 405 });
        }
    }


}
