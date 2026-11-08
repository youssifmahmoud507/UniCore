using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Caching
{
    // What is stored in the cache for a user. UserAccess itself has an IReadOnlySet, which System.Text.Json cannot read
    // back, so the cache keeps this plain shape and converts.
    public sealed record CachedUserAccess(Guid UserId, List<CachedRoleGrant> Grants)
    {
        public static CachedUserAccess From(UserAccess access) => new(
            access.UserId, [.. access.Grants.Select(g => new CachedRoleGrant(g.Role, [.. g.Permissions], [.. g.Scopes.Select(s => new CachedScopeGrant(s.ScopeType, s.ScopeId))]))]);

        public UserAccess ToUserAccess() => new(UserId, [.. Grants.Select(g => new RoleGrant(g.Role, new HashSet<string>(g.Permissions, StringComparer.Ordinal), [.. g.Scopes.Select(s => new ScopeGrant(s.ScopeType, s.ScopeId))]))]);
    }



}
