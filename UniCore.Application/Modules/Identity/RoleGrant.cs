using UniCore.Application.Common.Abstractions;

namespace UniCore.Application.Modules.Identity
{
    public sealed record RoleGrant(string Role,IReadOnlySet<string> Permissions,IReadOnlyList<ScopeGrant> Scopes);



}
