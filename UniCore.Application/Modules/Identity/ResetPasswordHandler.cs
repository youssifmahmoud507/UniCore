using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class ResetPasswordHandler(IPasswordResetAccountService accounts, IRepository<RefreshToken> refreshTokens, IRepository<PasswordResetOtp> otps, IUnitOfWork unitOfWork, IClock clock, IEmailQueue emailQueue, IAuditWriter audit, IPasswordResetSettings settings)
    {
        private readonly IPasswordResetAccountService _accounts = accounts;
        private readonly IRepository<RefreshToken> _refreshTokens = refreshTokens;
        private readonly IRepository<PasswordResetOtp> _otps = otps;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IEmailQueue _emailQueue = emailQueue;
        private readonly IAuditWriter _audit = audit;
        private readonly IPasswordResetSettings _settings = settings;

        public async Task<Result> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default)
        {
            var email = command.Email?.Trim();
            var token = command.ResetToken?.Trim();

            if (email is null || token is null || token.Length == 0 || !EmailAddressValidator.IsValid(email))
            {
                return Result.Fail(IdentityErrors.ResetTokenInvalid);
            }

            if (string.IsNullOrEmpty(command.NewPassword))
            {
                return Result.Fail(IdentityErrors.PasswordPolicyViolation);
            }

            if (!string.Equals(command.NewPassword, command.ConfirmPassword, StringComparison.Ordinal))
            {
                return Result.Fail(IdentityErrors.PasswordMismatch);
            }

            var found = await _accounts.FindActiveByEmailAsync(email);
            if (!found.TryGetValue(out var account))
            {
                return Result.Fail(IdentityErrors.ResetTokenInvalid);
            }

            // 1) Validate without side effects, so a bad token or a weak password changes nothing.
            var validation = await _accounts.ValidateResetAsync(account.UserId, token, command.NewPassword);
            if (validation.IsFailure)
            {
                return validation;
            }

            // 2) Revoke every session first. If a later step fails, the safe outcome is
            //    "sessions revoked, password unchanged", never "password changed, old sessions alive".
            var now = _clock.UtcNow;

            var sessions = await _refreshTokens.ListAsync(
                new ActiveRefreshTokensByUserSpecification(account.UserId, now), cancellationToken);
            foreach (var session in sessions)
            {
                session.Revoke(now, command.IpAddress); // cannot fail: the query returns only non-revoked tokens
            }

            var otps = await _otps.ListAsync(new ActiveOtpsByUserSpecification(account.UserId, now), cancellationToken);
            foreach (var otp in otps)
            {
                otp.Invalidate(now);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Fail(saved.Errors);
            }

            // 3) Change the password and clear the lockout state.
            var applied = await _accounts.ApplyResetAsync(account.UserId, token, command.NewPassword);
            if (applied.IsFailure)
            {
                return applied;
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.AuthPasswordResetCompleted, account.UserId, "User", account.UserId.ToString(),
                    command.IpAddress, command.UserAgent),
                cancellationToken);

            if (!_emailQueue.TryEnqueue(AuthEmails.PasswordChanged(account.Email, _settings.AppName)))
            {
                await _audit.WriteAsync(
                    new AuditEntry(AuditActions.EmailEnqueueFailed, account.UserId, "User", account.UserId.ToString(),
                        command.IpAddress, command.UserAgent),
                    cancellationToken);
            }

            return Result.Ok();
        }
    }


}
