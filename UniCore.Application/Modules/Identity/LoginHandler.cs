using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class LoginHandler(IUserAccountService accounts, ITokenService tokens, IRepository<RefreshToken> refreshTokens, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IUserAccountService _accounts = accounts;
        private readonly ITokenService _tokens = tokens;
        private readonly IRepository<RefreshToken> _refreshTokens = refreshTokens;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<AuthTokensResponse>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(command.Login) || string.IsNullOrEmpty(command.Password))
            {
                return IdentityErrors.CredentialsRequired;
            }

            var validated = await _accounts.ValidateCredentialsAsync(command.Login.Trim(), command.Password);

            if (!validated.TryGetValue(out var subject))
            {
                await AuditFailureAsync(validated.Error, command, cancellationToken);
                return Result<AuthTokensResponse>.Fail(validated.Errors);
            }

            var accessToken = _tokens.CreateAccessToken(subject);
            var generated = _tokens.GenerateRefreshToken();

            var created = RefreshToken.Create(subject.UserId, generated.TokenHash, _clock.UtcNow, generated.ExpiresAt, command.IpAddress);

            if (!created.TryGetValue(out var refreshToken))
            {
                return Result<AuthTokensResponse>.Fail(created.Errors);
            }

            _refreshTokens.Add(refreshToken);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<AuthTokensResponse>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(new AuditEntry(AuditActions.AuthLoginSucceeded, subject.UserId, "User", subject.UserId.ToString(),command.IpAddress, command.UserAgent),cancellationToken);

            return new AuthTokensResponse(accessToken.Token, accessToken.ExpiresAt, generated.Token, generated.ExpiresAt);
        }

        private async Task AuditFailureAsync(Error? error, LoginCommand command, CancellationToken cancellationToken)
        {
            string? action = error?.Code switch
            {
                "INVALID_CREDENTIALS" => AuditActions.AuthLoginFailed,
                "ACCOUNT_LOCKED_OUT" => AuditActions.AuthAccountLocked,
                _ => null // infrastructure failures are not login attempts worth auditing
            };

            if (action is null)
            {
                return;
            }

            // The attempted login name is not recorded: it is personal data and may be a typo of a real one.
            await _audit.WriteAsync(
                new AuditEntry(action, IpAddress: command.IpAddress, UserAgent: command.UserAgent),
                cancellationToken);
        }
    }
}
