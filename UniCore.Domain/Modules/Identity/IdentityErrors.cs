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
        public static readonly Error OtpDataInvalid = Error.Validation("OTP_DATA_INVALID", "The one-time password data is invalid.");
        public static readonly Error ResetTokenInvalid = Error.Validation("RESET_TOKEN_INVALID", "The reset token is invalid or has expired.");
        public static readonly Error PasswordMismatch =Error.Validation("PASSWORD_MISMATCH", "The passwords do not match.");
        public static readonly Error ForbiddenPermissionMissing = Error.Forbidden("FORBIDDEN_PERMISSION_MISSING", "You do not have the required permission.");
        public static readonly Error ForbiddenOutOfScope = Error.Forbidden("FORBIDDEN_OUT_OF_SCOPE", "The resource is outside your scope.");
        public static readonly Error ForbiddenNotOwner = Error.Forbidden("FORBIDDEN_NOT_OWNER", "You do not own this resource.");
        public static readonly Error PermissionCodeInvalid = Error.Validation("PERMISSION_CODE_INVALID", "A permission code must look like Module.Action.");
        public static readonly Error PermissionDataInvalid = Error.Validation("PERMISSION_DATA_INVALID", "The permission data is invalid.");
        public static readonly Error RolePermissionDataInvalid = Error.Validation("ROLE_PERMISSION_DATA_INVALID", "The role permission data is invalid.");
        public static readonly Error ScopeInvalid = Error.Validation("SCOPE_INVALID", "The scope is invalid.");
        public static readonly Error RoleNotFound = Error.NotFound("ROLE_NOT_FOUND", "The role was not found.");
        public static readonly Error UserNotFound = Error.NotFound("USER_NOT_FOUND", "The user was not found.");
        public static readonly Error ScopeAssignmentNotFound = Error.NotFound("SCOPE_ASSIGNMENT_NOT_FOUND", "The scope assignment was not found.");
        public static readonly Error RoleProtected = Error.BusinessRule("ROLE_PROTECTED", "Built-in roles cannot be deleted.");
        public static readonly Error RoleImmutable = Error.BusinessRule("ROLE_IMMUTABLE", "The permissions of the SuperAdmin role cannot be changed.");
        public static readonly Error RoleInUse = Error.Conflict("ROLE_IN_USE", "The role is still assigned to users.");
        public static readonly Error LastSuperAdmin = Error.BusinessRule("LAST_SUPERADMIN", "There must be at least one active SuperAdmin.");
        public static readonly Error CannotModifyOwnAccess = Error.Forbidden("CANNOT_MODIFY_OWN_ACCESS", "You cannot change your own roles, scopes or status.");
        public static readonly Error OnlySuperAdminManagesSuperAdmin = Error.Forbidden("ONLY_SUPERADMIN_CAN_MANAGE_SUPERADMIN", "Only a SuperAdmin can manage the SuperAdmin role.");
        public static readonly Error CannotGrantGlobalScope = Error.Forbidden("ONLY_SUPERADMIN_CAN_GRANT_GLOBAL_SCOPE", "Only a SuperAdmin can grant a Global scope.");
        public static readonly Error CannotGrantPermission = Error.Forbidden("CANNOT_GRANT_PERMISSION_YOU_LACK", "You cannot grant a permission you do not hold yourself.");
        public static readonly Error PermissionsRequired = Error.Validation("PERMISSIONS_REQUIRED", "A list of permission codes is required.");
        public static Error PermissionNotFound(IEnumerable<string> codes) => Error.Validation("PERMISSION_NOT_FOUND","Unknown permission code(s): " + string.Join(", ", codes.Take(5).Select(c => c.Length > 60 ? c[..60] : c)));


    }
}