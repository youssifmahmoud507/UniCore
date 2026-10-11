using Microsoft.AspNetCore.Authorization;

namespace UniCore.Api.Security
{
    public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
    {
        public string Permission { get; } = permission;
    }

}
