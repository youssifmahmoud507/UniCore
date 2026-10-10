using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    public class AuthHandlersTests
    {
        // ---------- Login ----------

        [Fact]
        public async Task Login_success_returns_tokens_stores_only_the_hash_and_audits()
        {
            var env = new AuthTestEnv();

            var tokens = await env.LoginOkAsync();

            Assert.Equal("Bearer", tokens.TokenType);
            var stored = Assert.Single(env.RefreshTokens.Items);
            Assert.Equal(env.UserId, stored.UserId);
            Assert.Equal("hash:" + tokens.RefreshToken, stored.TokenHash);
            Assert.NotEqual(tokens.RefreshToken, stored.TokenHash);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthLoginSucceeded && e.ActorUserId == env.UserId);
        }

        [Theory]
        [InlineData(null, "pw")]
        [InlineData("", "pw")]
        [InlineData("   ", "pw")]
        [InlineData("ahmed", null)]
        [InlineData("ahmed", "")]
        public async Task Login_without_credentials_fails_without_touching_the_account_service(string? login, string? password)
        {
            var env = new AuthTestEnv();

            var result = await env.Login.HandleAsync(new LoginCommand(login, password, null, null));

            Assert.True(result.IsFailure);
            Assert.Equal("CREDENTIALS_REQUIRED", result.Error?.Code);
            Assert.Equal(0, env.Accounts.ValidateCalls);
        }

        [Fact]
        public async Task Login_with_invalid_credentials_audits_the_failure_and_stores_nothing()
        {
            var env = new AuthTestEnv();
            env.Accounts.ValidateResult = Result<TokenSubject>.Fail(IdentityErrors.InvalidCredentials);

            var result = await env.Login.HandleAsync(new LoginCommand("ahmed", "wrong", "10.0.0.1", "agent"));

            Assert.True(result.IsFailure);
            Assert.Equal("INVALID_CREDENTIALS", result.Error?.Code);
            Assert.Empty(env.RefreshTokens.Items);
            var entry = Assert.Single(env.Audit.Entries);
            Assert.Equal(AuditActions.AuthLoginFailed, entry.Action);
            Assert.Null(entry.ActorUserId);
        }

        [Fact]
        public async Task Login_on_a_locked_account_returns_the_lockout_error_and_audits_it()
        {
            var env = new AuthTestEnv();
            env.Accounts.ValidateResult = Result<TokenSubject>.Fail(IdentityErrors.AccountLockedOut);

            var result = await env.Login.HandleAsync(new LoginCommand("ahmed", "pw", null, null));

            Assert.Equal("ACCOUNT_LOCKED_OUT", result.Error?.Code);
            Assert.Equal(AuditActions.AuthAccountLocked, Assert.Single(env.Audit.Entries).Action);
        }

        [Fact]
        public async Task Login_when_the_account_service_is_down_is_not_audited_as_a_failed_login()
        {
            var env = new AuthTestEnv();
            env.Accounts.ValidateResult = Result<TokenSubject>.Fail(CommonErrors.ExternalServiceUnavailable);

            var result = await env.Login.HandleAsync(new LoginCommand("ahmed", "pw", null, null));

            Assert.Equal("EXTERNAL_SERVICE_UNAVAILABLE", result.Error?.Code);
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task Login_when_saving_fails_returns_the_failure_and_no_success_audit()
        {
            var env = new AuthTestEnv();
            env.Uow.FailWith = CommonErrors.DatabaseUpdateFailed;

            var result = await env.Login.HandleAsync(new LoginCommand("ahmed", "pw", null, null));

            Assert.True(result.IsFailure);
            Assert.DoesNotContain(env.Audit.Entries, e => e.Action == AuditActions.AuthLoginSucceeded);
        }

        // ---------- Refresh ----------

        [Fact]
        public async Task Refresh_rotates_the_token()
        {
            var env = new AuthTestEnv();
            var first = await env.LoginOkAsync();

            var result = await env.RefreshAsync(first.RefreshToken);

            Assert.True(result.TryGetValue(out var second));
            Assert.NotNull(second);
            Assert.NotEqual(first.RefreshToken, second.RefreshToken);
            Assert.Equal(2, env.RefreshTokens.Items.Count);

            var old = env.RefreshTokens.Items.Single(t => t.TokenHash == "hash:" + first.RefreshToken);
            var current = env.RefreshTokens.Items.Single(t => t.TokenHash == "hash:" + second.RefreshToken);
            Assert.True(old.IsRevoked);
            Assert.Equal(current.Id, old.ReplacedByTokenId);
            Assert.True(current.IsActive(env.Clock.UtcNow));
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthTokenRefreshed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("never-issued")]
        public async Task Refresh_with_missing_or_unknown_token_fails(string? rawToken)
        {
            var env = new AuthTestEnv();

            var result = await env.RefreshAsync(rawToken);

            Assert.Equal("REFRESH_TOKEN_INVALID", result.Error?.Code);
        }

        [Fact]
        public async Task Refresh_with_an_expired_token_fails()
        {
            var env = new AuthTestEnv();
            var tokens = await env.LoginOkAsync();

            env.Clock.Advance(TimeSpan.FromDays(8));
            var result = await env.RefreshAsync(tokens.RefreshToken);

            Assert.Equal("REFRESH_TOKEN_EXPIRED", result.Error?.Code);
        }

        [Fact]
        public async Task Reusing_a_rotated_token_revokes_every_active_token_of_the_user()
        {
            var env = new AuthTestEnv();
            var a = await env.LoginOkAsync();
            var refreshed = await env.RefreshAsync(a.RefreshToken);          // A is now revoked, B is active
            Assert.True(refreshed.IsSuccess);
            await env.LoginOkAsync();                                         // C: a second active session

            var reuse = await env.RefreshAsync(a.RefreshToken);               // someone presents A again

            Assert.Equal("REFRESH_TOKEN_REUSED", reuse.Error?.Code);
            Assert.Equal(3, env.RefreshTokens.Items.Count);
            Assert.All(env.RefreshTokens.Items, t => Assert.True(t.IsRevoked));
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthTokenReuseDetected && e.ActorUserId == env.UserId);
        }

        [Fact]
        public async Task Refresh_for_an_inactive_account_revokes_the_token_and_fails()
        {
            var env = new AuthTestEnv();
            var tokens = await env.LoginOkAsync();
            env.Accounts.ActiveResult = Result<TokenSubject>.Fail(IdentityErrors.AccountInactive);

            var result = await env.RefreshAsync(tokens.RefreshToken);

            Assert.Equal("REFRESH_TOKEN_INVALID", result.Error?.Code);
            Assert.True(Assert.Single(env.RefreshTokens.Items).IsRevoked);
        }

        [Fact]
        public async Task Refresh_when_the_account_service_is_down_keeps_the_token_usable()
        {
            var env = new AuthTestEnv();
            var tokens = await env.LoginOkAsync();
            env.Accounts.ActiveResult = Result<TokenSubject>.Fail(CommonErrors.ExternalServiceUnavailable);

            var result = await env.RefreshAsync(tokens.RefreshToken);

            Assert.Equal("EXTERNAL_SERVICE_UNAVAILABLE", result.Error?.Code);
            Assert.True(Assert.Single(env.RefreshTokens.Items).IsActive(env.Clock.UtcNow));
        }

        [Fact]
        public async Task Refresh_returns_the_concurrency_conflict_when_the_save_loses_the_race()
        {
            var env = new AuthTestEnv();
            var tokens = await env.LoginOkAsync();
            env.Uow.FailWith = CommonErrors.ConcurrencyConflict;

            var result = await env.RefreshAsync(tokens.RefreshToken);

            Assert.Equal("CONCURRENCY_CONFLICT", result.Error?.Code);
        }

        // ---------- Logout ----------

        [Fact]
        public async Task Logout_revokes_the_users_own_token_and_audits()
        {
            var env = new AuthTestEnv();
            var tokens = await env.LoginOkAsync();

            var result = await env.Logout.HandleAsync(new LogoutCommand(tokens.RefreshToken, "10.0.0.1", "agent"));

            Assert.True(result.IsSuccess);
            Assert.True(Assert.Single(env.RefreshTokens.Items).IsRevoked);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthLogout && e.ActorUserId == env.UserId);
        }

        [Fact]
        public async Task Logout_with_someone_elses_token_succeeds_but_revokes_nothing()
        {
            var env = new AuthTestEnv();
            var tokens = await env.LoginOkAsync();
            env.CurrentUser.UserId = Guid.NewGuid(); // a different logged-in user

            var result = await env.Logout.HandleAsync(new LogoutCommand(tokens.RefreshToken, null, null));

            Assert.True(result.IsSuccess);
            Assert.False(Assert.Single(env.RefreshTokens.Items).IsRevoked);
        }

        [Fact]
        public async Task Logout_is_idempotent_for_unknown_and_already_revoked_tokens()
        {
            var env = new AuthTestEnv();
            var tokens = await env.LoginOkAsync();
            await env.Logout.HandleAsync(new LogoutCommand(tokens.RefreshToken, null, null));

            var again = await env.Logout.HandleAsync(new LogoutCommand(tokens.RefreshToken, null, null));
            var unknown = await env.Logout.HandleAsync(new LogoutCommand("never-issued", null, null));

            Assert.True(again.IsSuccess);
            Assert.True(unknown.IsSuccess);
        }

        [Fact]
        public async Task Logout_without_an_authenticated_user_fails()
        {
            var env = new AuthTestEnv();
            env.CurrentUser.UserId = null;

            var result = await env.Logout.HandleAsync(new LogoutCommand("anything", null, null));

            Assert.Equal("GENERAL_UNAUTHORIZED", result.Error?.Code);
        }

        [Fact]
        public async Task Logout_without_a_token_is_a_validation_error()
        {
            var env = new AuthTestEnv();

            var result = await env.Logout.HandleAsync(new LogoutCommand(null, null, null));

            Assert.Equal("REFRESH_TOKEN_REQUIRED", result.Error?.Code);
        }
    }
}
