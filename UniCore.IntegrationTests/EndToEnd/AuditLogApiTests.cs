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

}
