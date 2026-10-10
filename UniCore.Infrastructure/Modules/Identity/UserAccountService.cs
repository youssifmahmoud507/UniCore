using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class UserAccountService(UserManager<ApplicationUser> userManager, IClock clock, ILogger<UserAccountService> logger) : IUserAccountService
    {
        // Used only to spend the same time on unknown/inactive accounts as on a real password check.
        private static readonly ApplicationUser? DummyUser = ApplicationUser.Create("dummy", "dummy@invalid.local", null, DateTimeOffset.UnixEpoch).Value;

        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly IClock _clock = clock;
        private readonly ILogger<UserAccountService> _logger = logger;

        public async Task<Result<TokenSubject>> ValidateCredentialsAsync(string login, string password)
        {
            try
            {
                var user = await FindByLoginAsync(login);

                if (user is null || !user.IsActive)
                {
                    BurnTime(password);
                    return IdentityErrors.InvalidCredentials;
                }

                if (await _userManager.IsLockedOutAsync(user))
                {
                    return IdentityErrors.AccountLockedOut;
                }

                if (!await _userManager.CheckPasswordAsync(user, password))
                {
                    // Counts the failure and locks the account when the limit is reached.
                    await _userManager.AccessFailedAsync(user);

                    return await _userManager.IsLockedOutAsync(user)
                        ? IdentityErrors.AccountLockedOut
                        : IdentityErrors.InvalidCredentials;
                }

                await _userManager.ResetAccessFailedCountAsync(user);

                user.RecordLogin(_clock.UtcNow);
                var updated = (await _userManager.UpdateAsync(user)).ToResult();
                if (updated.IsFailure)
                {
                    return Result<TokenSubject>.Fail(updated.Errors);
                }

                return await BuildSubjectAsync(user);
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex)
            {
                _logger.LogError("Credential validation failed ({ExceptionType}).", ex.GetType().Name);
                return CommonErrors.ExternalServiceUnavailable;
            }
        }

        public async Task<Result<TokenSubject>> GetActiveSubjectAsync(Guid userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());

                if (user is null || !user.IsActive)
                {
                    return IdentityErrors.AccountInactive;
                }

                if (await _userManager.IsLockedOutAsync(user))
                {
                    return IdentityErrors.AccountLockedOut;
                }

                return await BuildSubjectAsync(user);
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex)
            {
                _logger.LogError("Loading the account failed ({ExceptionType}).", ex.GetType().Name);
                return CommonErrors.ExternalServiceUnavailable;
            }
        }

        private Task<ApplicationUser?> FindByLoginAsync(string login) => login.Contains('@') ? _userManager.FindByEmailAsync(login) : _userManager.FindByNameAsync(login);

        private async Task<Result<TokenSubject>> BuildSubjectAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            // PersonId stays null until PersonProfile is linked to the account (People module).
            return new TokenSubject(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty, null, roles.ToArray());
        }

        private void BurnTime(string password)
        {
            if (DummyUser is not null)
            {
                _ = _userManager.PasswordHasher.HashPassword(DummyUser, password);
            }
        }
    }

}
