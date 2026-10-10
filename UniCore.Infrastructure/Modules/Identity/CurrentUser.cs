using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;

        public CurrentUser(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

        public Guid? UserId => ReadGuid(AppClaimTypes.Sub);

        public Guid? PersonId => ReadGuid(AppClaimTypes.PersonId);

        public IReadOnlyCollection<string> Roles
            => !IsAuthenticated || Principal is null
                ? Array.Empty<string>()
                : Principal.FindAll(AppClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();

        // Permissions and scopes come from the authorization layer (RBAC batch).
        // Until then both are empty, so HasPermission is false: it fails closed.
        public IReadOnlyCollection<string> Permissions => Array.Empty<string>();

        public IReadOnlyCollection<ScopeGrant> Scopes => Array.Empty<ScopeGrant>();

        public bool IsInRole(string role)
            => Roles.Contains(role, StringComparer.Ordinal);

        public bool HasPermission(string permission)
            => IsAuthenticated && Permissions.Contains(permission, StringComparer.Ordinal);

        private Guid? ReadGuid(string claimType)
        {
            if (!IsAuthenticated)
            {
                return null;
            }

            var value = Principal?.FindFirst(claimType)?.Value;

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

}
