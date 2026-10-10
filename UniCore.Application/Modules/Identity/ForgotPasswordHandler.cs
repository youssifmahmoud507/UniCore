using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class ForgotPasswordHandler(IPasswordResetAccountService accounts,IOtpService otpService,IRepository<PasswordResetOtp> otps,IUnitOfWork unitOfWork,IClock clock,IEmailQueue emailQueue,IAuditWriter audit,IPasswordResetSettings settings)
    {
        private readonly IPasswordResetAccountService _accounts = accounts;
        private readonly IOtpService _otpService = otpService;
        private readonly IRepository<PasswordResetOtp> _otps = otps;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IEmailQueue _emailQueue = emailQueue;
        private readonly IAuditWriter _audit = audit;
        private readonly IPasswordResetSettings _settings = settings;

        // Every outcome for a well-formed email is Result.Ok, so the response never reveals
        // whether the account exists. Internal problems are audited instead.
        public async Task<Result> HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken = default)
        {
            var email = command.Email?.Trim();
            if (email is null || !EmailAddressValidator.IsValid(email))
            {
                return Result.Fail(IdentityErrors.UserEmailInvalid);
            }

            var now = _clock.UtcNow;

            var found = await _accounts.FindActiveByEmailAsync(email);
            if (!found.TryGetValue(out var account))
            {
                // Unknown or inactive account: similar CPU work, nothing stored, nothing sent.
                _ = _otpService.Hash(Guid.Empty, _otpService.Generate());
                return Result.Ok();
            }

            var recent = await _otps.CountAsync(
                new OtpRequestsSinceSpecification(account.UserId, now.AddHours(-1)), cancellationToken);
            if (recent >= _settings.MaxRequestsPerHour)
            {
                await AuditAsync(AuditActions.AuthPasswordResetThrottled, account.UserId, command, cancellationToken);
                return Result.Ok();
            }

            var latest = await _otps.FirstOrDefaultAsync(
                new LatestUnconsumedOtpByUserSpecification(account.UserId), cancellationToken);
            if (latest is not null && latest.ResendAvailableAt > now)
            {
                await AuditAsync(AuditActions.AuthPasswordResetThrottled, account.UserId, command, cancellationToken);
                return Result.Ok();
            }

            var previous = await _otps.ListAsync(
                new ActiveOtpsByUserSpecification(account.UserId, now), cancellationToken);
            foreach (var old in previous)
            {
                old.Invalidate(now);
            }

            var otp = _otpService.Generate();

            var created = PasswordResetOtp.Create(
                account.UserId,
                _otpService.Hash(account.UserId, otp),
                now,
                TimeSpan.FromMinutes(_settings.OtpLifetimeMinutes),
                _settings.MaxAttempts,
                TimeSpan.FromSeconds(_settings.ResendCooldownSeconds),
                command.IpAddress);

            if (!created.TryGetValue(out var entity))
            {
                return Result.Fail(created.Errors);
            }

            _otps.Add(entity);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                await AuditAsync(AuditActions.AuthPasswordResetRequestFailed, account.UserId, command, cancellationToken);
                return Result.Ok();
            }

            var queued = _emailQueue.TryEnqueue(
                AuthEmails.PasswordResetOtp(account.Email, _settings.AppName, otp, _settings.OtpLifetimeMinutes));

            await AuditAsync(
                queued ? AuditActions.AuthPasswordResetRequested : AuditActions.EmailEnqueueFailed,
                account.UserId, command, cancellationToken);

            return Result.Ok();
        }

        private Task AuditAsync(string action, Guid userId, ForgotPasswordCommand command, CancellationToken cancellationToken) => _audit.WriteAsync(new AuditEntry(action, userId, "User", userId.ToString(), command.IpAddress, command.UserAgent),cancellationToken);
    }


}
