using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class PasswordResetTestEnv
    {
        public const string Email = "ahmed@uni.edu";

        public Guid UserId { get; } = Guid.NewGuid();
        public FakeClock Clock { get; } = new(AuthTestEnv.Start);
        public TestPasswordResetSettings Settings { get; } = new();
        public FakeOtpService Otp { get; } = new();
        public FakePasswordResetAccountService Accounts { get; } = new();
        public InMemoryRepository<PasswordResetOtp> Otps { get; } = new();
        public InMemoryRepository<RefreshToken> RefreshTokens { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public FakeEmailQueue Emails { get; } = new();
        public FakeAuditWriter Audit { get; } = new();

        public ForgotPasswordHandler Forgot { get; }
        public VerifyOtpHandler Verify { get; }
        public ResetPasswordHandler Reset { get; }

        public PasswordResetTestEnv()
        {
            Accounts.Accounts[Email] = UserId;

            Forgot = new ForgotPasswordHandler(Accounts, Otp, Otps, Uow, Clock, Emails, Audit, Settings);
            Verify = new VerifyOtpHandler(Accounts, Otp, Otps, Uow, Clock, Audit, Settings);
            Reset = new ResetPasswordHandler(Accounts, RefreshTokens, Otps, Uow, Clock, Emails, Audit, Settings);
        }

        public Task<Result> ForgotAsync(string email = Email)
            => Forgot.HandleAsync(new ForgotPasswordCommand(email, "10.0.0.1", "agent"));

        public Task<Result<VerifyOtpResponse>> VerifyAsync(string otp, string email = Email)
            => Verify.HandleAsync(new VerifyOtpCommand(email, otp, "10.0.0.1", "agent"));

        public Task<Result> ResetAsync(
            string newPassword = "NewP@ssw0rd!!", string confirm = "NewP@ssw0rd!!", string token = "reset-token", string email = Email)
            => Reset.HandleAsync(new ResetPasswordCommand(email, token, newPassword, confirm, "10.0.0.2", "agent"));

        public RefreshToken AddActiveRefreshToken()
        {
            var created = RefreshToken.Create(UserId, Guid.NewGuid().ToString("N"), Clock.UtcNow, Clock.UtcNow.AddDays(7), null);

            Assert.True(created.TryGetValue(out var token));
            Assert.NotNull(token);
            RefreshTokens.Add(token);
            return token;
        }
    }



}
