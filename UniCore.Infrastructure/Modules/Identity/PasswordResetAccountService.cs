using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class PasswordResetAccountService : IPasswordResetAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PasswordResetAccountService> _logger;

        public PasswordResetAccountService(
            UserManager<ApplicationUser> userManager, ILogger<PasswordResetAccountService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<Result<ResetCandidate>> FindActiveByEmailAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user is null || !user.IsActive || string.IsNullOrEmpty(user.Email))
                {
                    return Error.NotFound("USER_NOT_FOUND", "User not found.");
                }

                return new ResetCandidate(user.Id, user.Email);
            }
            catch (Exception ex)
            {
                return Fail<ResetCandidate>(ex);
            }
        }

        public async Task<Result<string>> GenerateResetTokenAsync(Guid userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user is null)
                {
                    return IdentityErrors.ResetTokenInvalid;
                }

                return await _userManager.GeneratePasswordResetTokenAsync(user);
            }
            catch (Exception ex)
            {
                return Fail<string>(ex);
            }
        }

        public async Task<Result> ValidateResetAsync(Guid userId, string resetToken, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user is null)
                {
                    return Result.Fail(IdentityErrors.ResetTokenInvalid);
                }

                var tokenValid = await _userManager.VerifyUserTokenAsync(
                    user,
                    _userManager.Options.Tokens.PasswordResetTokenProvider,
                    UserManager<ApplicationUser>.ResetPasswordTokenPurpose,
                    resetToken);

                if (!tokenValid)
                {
                    return Result.Fail(IdentityErrors.ResetTokenInvalid);
                }

                foreach (var validator in _userManager.PasswordValidators)
                {
                    var check = await validator.ValidateAsync(_userManager, user, newPassword);
                    if (!check.Succeeded)
                    {
                        return check.ToResult();
                    }
                }

                return Result.Ok();
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        public async Task<Result> ApplyResetAsync(Guid userId, string resetToken, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user is null)
                {
                    return Result.Fail(IdentityErrors.ResetTokenInvalid);
                }

                // Changing the password also changes the security stamp, which makes this token single use.
                var reset = (await _userManager.ResetPasswordAsync(user, resetToken, newPassword)).ToResult();
                if (reset.IsFailure)
                {
                    return reset;
                }

                await _userManager.ResetAccessFailedCountAsync(user);
                await _userManager.SetLockoutEndDateAsync(user, null);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        private Result Fail(Exception ex)
        {
            _logger.LogError("Password reset operation failed ({ExceptionType}).", ex.GetType().Name);
            return Result.Fail(CommonErrors.ExternalServiceUnavailable);
        }

        private Result<T> Fail<T>(Exception ex)
        {
            _logger.LogError("Password reset operation failed ({ExceptionType}).", ex.GetType().Name);
            return Result<T>.Fail(CommonErrors.ExternalServiceUnavailable);
        }
    }




}
