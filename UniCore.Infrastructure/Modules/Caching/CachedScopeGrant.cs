using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Caching
{
    public sealed record CachedScopeGrant(ScopeType ScopeType, Guid? ScopeId);



}
