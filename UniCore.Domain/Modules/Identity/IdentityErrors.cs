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
        public static readonly Error RefreshTokenDataInvalid = Error.Validation("REFRESH_TOKEN_DATA_INVALID", "The refresh token data is invalid.");
        public static readonly Error RefreshTokenInvalid = Error.Unauthorized("REFRESH_TOKEN_INVALID", "The refresh token is invalid.");
        public static readonly Error RefreshTokenExpired = Error.Unauthorized("REFRESH_TOKEN_EXPIRED", "The refresh token has expired.");
        public static readonly Error RefreshTokenReused = Error.Unauthorized("REFRESH_TOKEN_REUSED", "The refresh token was already used.");
        public static readonly Error RefreshTokenAlreadyRevoked = Error.BusinessRule("REFRESH_TOKEN_ALREADY_REVOKED", "The refresh token is already revoked.");
        public static readonly Error CredentialsRequired = Error.Validation("CREDENTIALS_REQUIRED", "Login and password are required.");
        public static readonly Error InvalidCredentials = Error.InvalidCredentials();
        public static readonly Error AccountLockedOut = Error.Forbidden("ACCOUNT_LOCKED_OUT", "The account is temporarily locked. Try again later.");
        public static readonly Error AccountInactive = Error.Forbidden("ACCOUNT_INACTIVE", "The account is inactive.");
        public static readonly Error RefreshTokenRequired = Error.Validation("REFRESH_TOKEN_REQUIRED", "A refresh token is required.");
        public static readonly Error OtpInvalid = Error.Validation("OTP_INVALID", "The code is invalid or has expired.");
        public static readonly Error OtpDataInvalid =
            Error.Validation("OTP_DATA_INVALID", "The one-time password data is invalid.");
        public static readonly Error ResetTokenInvalid =
            Error.Validation("RESET_TOKEN_INVALID", "The reset token is invalid or has expired.");
        public static readonly Error PasswordMismatch =
            Error.Validation("PASSWORD_MISMATCH", "The passwords do not match.");
    }
}