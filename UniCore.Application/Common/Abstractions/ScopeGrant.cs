using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Common.Abstractions
{
    public sealed record ScopeGrant(ScopeType ScopeType, Guid? ScopeId);
}
