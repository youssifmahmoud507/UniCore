using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed record ScopeAssignmentInfo(Guid Id, string Role, ScopeType ScopeType, Guid? ScopeId, DateTimeOffset CreatedAt);



}
