using Microsoft.AspNetCore.Authorization;

namespace UniCore.Api.Security
{
    // [HasPermission(Permissions.Users.Manage)] on a controller or action.
    // For a check against a specific resource, use IPermissionAuthorizer inside the handler.
    public sealed class HasPermissionAttribute : AuthorizeAttribute
    {
        public const string PolicyPrefix = "Permission: ";

        public HasPermissionAttribute(string permission)
        {
            Policy = PolicyPrefix + permission;
        }
    }

}
