using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Identity
{
    public static class IdentityErrors
    {
        public static readonly Error UserNameRequired = Error.Validation("USER_NAME_REQUIRED", "User name is required.");
        public static readonly Error UserEmailInvalid = Error.Validation("USER_EMAIL_INVALID", "A valid email address is required.");
        public static readonly Error UserAlreadyExists = Error.Conflict("USER_ALREADY_EXISTS", "A user with the same user name or email already exists.");
        public static readonly Error RoleNameRequired = Error.Validation("ROLE_NAME_REQUIRED", "Role name is required.");
        public static readonly Error RoleAlreadyExists = Error.Conflict("ROLE_ALREADY_EXISTS", "A role with the same name already exists.");
        public static readonly Error PasswordPolicyViolation = Error.Validation("PASSWORD_POLICY_VIOLATION", "The password does not meet the password policy.");
        public static readonly Error IdentityOperationFailed = Error.Failure("IDENTITY_OPERATION_FAILED", "The identity operation could not be completed.");
    }
}
