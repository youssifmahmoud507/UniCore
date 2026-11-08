namespace UniCore.Infrastructure.Modules.Caching
{
    public sealed record CachedRoleGrant(string Role, List<string> Permissions, List<CachedScopeGrant> Scopes);



}
