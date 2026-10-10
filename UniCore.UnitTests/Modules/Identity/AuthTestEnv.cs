using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class AuthTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public Guid UserId { get; } = Guid.NewGuid();
        public FakeClock Clock { get; } = new(Start);
        public FakeUserAccountService Accounts { get; } = new();
        public FakeTokenService Tokens { get; }
        public InMemoryRepository<RefreshToken> RefreshTokens { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public FakeAuditWriter Audit { get; } = new();
        public FakeCurrentUser CurrentUser { get; } = new();

        public LoginHandler Login { get; }
        public RefreshHandler Refresh { get; }
        public LogoutHandler Logout { get; }

        public AuthTestEnv()
        {
            Tokens = new FakeTokenService(Clock);
            Accounts.ValidateResult = Result<TokenSubject>.Ok(Subject());
            Accounts.ActiveResult = Result<TokenSubject>.Ok(Subject());
            CurrentUser.UserId = UserId;

            Login = new LoginHandler(Accounts, Tokens, RefreshTokens, Uow, Clock, Audit);
            Refresh = new RefreshHandler(Accounts, Tokens, RefreshTokens, Uow, Clock, Audit);
            Logout = new LogoutHandler(CurrentUser, Tokens, RefreshTokens, Uow, Clock, Audit);
        }

        public TokenSubject Subject() => new(UserId, "ahmed", "ahmed@uni.edu", null, new[] { "Student" });

        public async Task<AuthTokensResponse> LoginOkAsync()
        {
            var result = await Login.HandleAsync(new LoginCommand("ahmed", "P@ssw0rd!!", "10.0.0.1", "test-agent"));

            Assert.True(result.TryGetValue(out var tokens));
            Assert.NotNull(tokens);
            return tokens;
        }

        public Task<Result<AuthTokensResponse>> RefreshAsync(string? rawToken) => Refresh.HandleAsync(new RefreshCommand(rawToken, "10.0.0.2", "test-agent"));
    }



}
