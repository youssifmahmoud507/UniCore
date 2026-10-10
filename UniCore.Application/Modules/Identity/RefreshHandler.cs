using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class RefreshHandler(IUserAccountService accounts, ITokenService tokens, IRepository<RefreshToken> refreshTokens, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IUserAccountService _accounts = accounts;
        private readonly ITokenService _tokens = tokens;
        private readonly IRepository<RefreshToken> _refreshTokens = refreshTokens;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<AuthTokensResponse>> HandleAsync(RefreshCommand command, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(command.RefreshToken))
            {
                return IdentityErrors.RefreshTokenInvalid;
            }

            var now = _clock.UtcNow;
            var hash = _tokens.HashRefreshToken(command.RefreshToken.Trim());

            var stored = await _refreshTokens.FirstOrDefaultAsync(
                new RefreshTokenByHashSpecification(hash), cancellationToken);

            if (stored is null)
            {
                return IdentityErrors.RefreshTokenInvalid;
            }

            // A token that was already used (or logged out) is being presented again: treat as theft.
            if (stored.IsRevoked)
            {
                return await HandleReuseAsync(stored, command, now, cancellationToken);
            }

            if (stored.IsExpired(now))
            {
                return IdentityErrors.RefreshTokenExpired;
            }

            var account = await _accounts.GetActiveSubjectAsync(stored.UserId);

            if (!account.TryGetValue(out var subject))
            {
                // A temporary infrastructure problem must not burn a valid token.
                if (account.Error?.ErrorType == ErrorType.External)
                {
                    return Result<AuthTokensResponse>.Fail(account.Errors);
                }

                // Account gone, inactive or locked: this token must not be usable any more.
                stored.Revoke(now, command.IpAddress); // cannot fail here: the token is not revoked yet
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return IdentityErrors.RefreshTokenInvalid;
            }

            var generated = _tokens.GenerateRefreshToken();

            var created = RefreshToken.Create(stored.UserId, generated.TokenHash, now, generated.ExpiresAt, command.IpAddress);
            if (!created.TryGetValue(out var next))
            {
                return Result<AuthTokensResponse>.Fail(created.Errors);
            }

            var revoked = stored.Revoke(now, command.IpAddress, next.Id);
            if (revoked.IsFailure)
            {
                return Result<AuthTokensResponse>.Fail(revoked.Errors);
            }

            _refreshTokens.Add(next);

            // Two parallel refreshes with the same token: the RowVersion makes the second save fail
            // with CONCURRENCY_CONFLICT, so only one new token can ever be issued.
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<AuthTokensResponse>.Fail(saved.Errors);
            }

            var accessToken = _tokens.CreateAccessToken(subject);

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.AuthTokenRefreshed, subject.UserId, "User", subject.UserId.ToString(),
                    command.IpAddress, command.UserAgent),
                cancellationToken);

            return new AuthTokensResponse(accessToken.Token, accessToken.ExpiresAt, generated.Token, generated.ExpiresAt);
        }

        private async Task<Result<AuthTokensResponse>> HandleReuseAsync(RefreshToken reused, RefreshCommand command, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var active = await _refreshTokens.ListAsync(
                new ActiveRefreshTokensByUserSpecification(reused.UserId, now), cancellationToken);

            foreach (var token in active)
            {
                token.Revoke(now, command.IpAddress); // cannot fail: the query only returns non-revoked tokens
            }

            // Even if this save fails the caller still gets the reuse error below, and the event is audited.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.AuthTokenReuseDetected, reused.UserId, "User", reused.UserId.ToString(),
                    command.IpAddress, command.UserAgent),
                cancellationToken);

            return IdentityErrors.RefreshTokenReused;
        }
    }
}
