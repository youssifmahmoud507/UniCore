using UniCore.Application.Common.Abstractions;

namespace UniCore.Api.Contracts.Me
{
    public sealed record MeResponse(Guid? UserId,Guid? PersonId,IReadOnlyCollection<string> Roles,IReadOnlyCollection<string> Permissions,IReadOnlyCollection<ScopeGrant> Scopes);

}
