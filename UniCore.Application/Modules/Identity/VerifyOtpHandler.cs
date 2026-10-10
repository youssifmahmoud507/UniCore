using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class VerifyOtpHandler(IPasswordResetAccountService accounts, IOtpService otpService, IRepository<PasswordResetOtp> otps, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit, IPasswordResetSettings settings)
    {
        private const int OtpLength = 6;

        private readonly IPasswordResetAccountService _accounts = accounts;
        private readonly IOtpService _otpService = otpService;
        private readonly IRepository<PasswordResetOtp> _otps = otps;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;
        private readonly IPasswordResetSettings _settings = settings;

        // Every failure returns OTP_INVALID. Different errors for "expired" or "locked" would tell
        // a caller that the account exists. The real reason goes to the audit log.
        public async Task<Result<VerifyOtpResponse>> HandleAsync(VerifyOtpCommand command, CancellationToken cancellationToken = default)
        {
            var email = command.Email?.Trim();
            var otp = command.Otp?.Trim();

            if (email is null || otp is null
                || !EmailAddressValidator.IsValid(email)
                || otp.Length != OtpLength
                || !otp.All(char.IsAsciiDigit))
            {
                return IdentityErrors.OtpInvalid;
            }

            var now = _clock.UtcNow;

            var found = await _accounts.FindActiveByEmailAsync(email);
            if (!found.TryGetValue(out var account))
            {
                _ = _otpService.Verify("0", Guid.Empty, otp);
                return IdentityErrors.OtpInvalid;
            }

            var stored = await _otps.FirstOrDefaultAsync(
                new LatestUnconsumedOtpByUserSpecification(account.UserId), cancellationToken);

            if (stored is null)
            {
                _ = _otpService.Verify("0", account.UserId, otp);
                await AuditAsync(AuditActions.AuthOtpFailed, account.UserId, command, cancellationToken);
                return IdentityErrors.OtpInvalid;
            }

            if (stored.IsLocked)
            {
                await AuditAsync(AuditActions.AuthOtpAttemptsExceeded, account.UserId, command, cancellationToken);
                return IdentityErrors.OtpInvalid;
            }

            if (stored.IsExpired(now))
            {
                await AuditAsync(AuditActions.AuthOtpExpired, account.UserId, command, cancellationToken);
                return IdentityErrors.OtpInvalid;
            }

            if (!_otpService.Verify(stored.OtpHash, account.UserId, otp))
            {
                var locked = stored.RegisterFailedAttempt();
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await AuditAsync(
                    locked ? AuditActions.AuthOtpAttemptsExceeded : AuditActions.AuthOtpFailed,
                    account.UserId, command, cancellationToken);

                return IdentityErrors.OtpInvalid;
            }

            // Create the reset token first: if consuming the OTP fails (for example a parallel
            // request won the race) the token is simply never handed out.
            var token = await _accounts.GenerateResetTokenAsync(account.UserId);
            if (!token.TryGetValue(out var resetToken))
            {
                return Result<VerifyOtpResponse>.Fail(token.Errors);
            }

            var consumed = stored.Consume(now);
            if (consumed.IsFailure)
            {
                return IdentityErrors.OtpInvalid;
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<VerifyOtpResponse>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.AuthOtpVerified, account.UserId, command, cancellationToken);

            return new VerifyOtpResponse(resetToken, now.AddMinutes(_settings.ResetTokenMinutes));
        }

        private Task AuditAsync(string action, Guid userId, VerifyOtpCommand command, CancellationToken cancellationToken) => _audit.WriteAsync(new AuditEntry(action, userId, "User", userId.ToString(), command.IpAddress, command.UserAgent), cancellationToken);
    }


}
