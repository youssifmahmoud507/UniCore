using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Identity
{
    public interface IUserAccountService
    {
        // Checks the password and the lockout state. On success it also resets the failed-attempt
        // counter and records LastLoginAt. Unknown, inactive and wrong-password all give INVALID_CREDENTIALS.
        Task<Result<TokenSubject>> ValidateCredentialsAsync(string login, string password);

        // Used by refresh: the account must still exist, be active and not be locked out.
        Task<Result<TokenSubject>> GetActiveSubjectAsync(Guid userId);
    }
}
