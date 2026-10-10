using Microsoft.AspNetCore.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public static class IdentityResultExtensions
    {
        public static Result ToResult(this IdentityResult result)
        {
            if (result.Succeeded)
            {
                return Result.Ok();
            }

            var errors = result.Errors.Select(Map).DistinctBy(e => e.Code).ToList();

            return errors.Count == 0
                ? Result.Fail(IdentityErrors.IdentityOperationFailed)
                : Result.Fail(errors);
        }

        private static Error Map(IdentityError error)
        {
            var code = error.Code ?? string.Empty;

            if (code.StartsWith("DuplicateUserName", StringComparison.Ordinal)
                || code.StartsWith("DuplicateEmail", StringComparison.Ordinal))
            {
                return IdentityErrors.UserAlreadyExists;
            }

            if (code.StartsWith("DuplicateRoleName", StringComparison.Ordinal))
            {
                return IdentityErrors.RoleAlreadyExists;
            }

            if (code.StartsWith("Password", StringComparison.Ordinal))
            {
                // Password rule descriptions are safe to show ("must have at least one digit").
                return Error.Validation(IdentityErrors.PasswordPolicyViolation.Code, error.Description ?? string.Empty);
            }

            if (code.StartsWith("InvalidEmail", StringComparison.Ordinal))
            {
                return IdentityErrors.UserEmailInvalid;
            }

            if (code.StartsWith("InvalidUserName", StringComparison.Ordinal))
            {
                return IdentityErrors.UserNameRequired;
            }

            if (code.StartsWith("ConcurrencyFailure", StringComparison.Ordinal))
            {
                return CommonErrors.ConcurrencyConflict;
            }

            // Unknown codes: never expose the raw Identity description.
            return IdentityErrors.IdentityOperationFailed;
        }
    }
}
