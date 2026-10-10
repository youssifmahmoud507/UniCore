using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class LogoutHandler(ICurrentUser currentUser, ITokenService tokens, IRepository<RefreshToken> refreshTokens, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly ITokenService _tokens = tokens;
        private readonly IRepository<RefreshToken> _refreshTokens = refreshTokens;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
        {
            if (_currentUser.UserId is not { } userId)
            {
                return Result.Fail(Error.Unauthorized());
            }

            if (string.IsNullOrWhiteSpace(command.RefreshToken))
            {
                return Result.Fail(IdentityErrors.RefreshTokenRequired);
            }

            var hash = _tokens.HashRefreshToken(command.RefreshToken.Trim());
            var stored = await _refreshTokens.FirstOrDefaultAsync(new RefreshTokenByHashSpecification(hash), cancellationToken);

            // Logout is idempotent. Unknown, already revoked or someone else's tokens are ignored
            // silently, so this endpoint can never be used to probe or kill another user's session.
            if (stored is null || stored.UserId != userId || stored.IsRevoked)
            {
                return Result.Ok();
            }

            var revoked = stored.Revoke(_clock.UtcNow, command.IpAddress);
            if (revoked.IsFailure)
            {
                return revoked;
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Fail(saved.Errors);
            }

            await _audit.WriteAsync(new AuditEntry(AuditActions.AuthLogout, userId, "User", userId.ToString(), command.IpAddress, command.UserAgent),cancellationToken);

            return Result.Ok();
        }
    }
}
