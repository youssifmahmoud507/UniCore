using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class CurrentUser(IHttpContextAccessor accessor, IUserAccessContext? access = null) : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor = accessor;
        private readonly IUserAccessContext? _access = access;

        private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

        public Guid? UserId => ReadGuid(AppClaimTypes.Sub);

        public Guid? PersonId => ReadGuid(AppClaimTypes.PersonId);

        // Live roles from the database once the access middleware ran. The role claims inside the token
        // can be up to 15 minutes old, so they are only a fallback.
        public IReadOnlyCollection<string> Roles
        {
            get
            {
                if (!IsAuthenticated)
                {
                    return [];
                }

                if (_access?.Current is { } access)
                {
                    return [.. access.Grants.Select(g => g.Role).Distinct(StringComparer.Ordinal)];
                }

                return Principal is null
                    ? []
                    : Principal.FindAll(AppClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();
            }
        }
        // Loaded per request from the database by the access middleware, never from the token.
        public IReadOnlyCollection<string> Permissions => !IsAuthenticated || _access?.Current is null ? [] : _access.Current.Grants.SelectMany(g => g.Permissions).Distinct(StringComparer.Ordinal).ToArray();

        public IReadOnlyCollection<ScopeGrant> Scopes => !IsAuthenticated || _access?.Current is null ? Array.Empty<ScopeGrant>() : [.. _access.Current.Grants.SelectMany(g => g.Scopes).Distinct()];

        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.Ordinal);

        public bool HasPermission(string permission) => IsAuthenticated && Permissions.Contains(permission, StringComparer.Ordinal);
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
