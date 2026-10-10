using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Identity
{
    public interface IPasswordResetAccountService
    {
        // NotFound for unknown and inactive accounts alike.
        Task<Result<ResetCandidate>> FindActiveByEmailAsync(string email);

        Task<Result<string>> GenerateResetTokenAsync(Guid userId);

        // Checks the reset token and the password policy without changing anything.
        Task<Result> ValidateResetAsync(Guid userId, string resetToken, string newPassword);

        // Sets the password and clears the lockout state.
        Task<Result> ApplyResetAsync(Guid userId, string resetToken, string newPassword);
    }


}
