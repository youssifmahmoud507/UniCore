using UniCore.Domain.Modules.Identity;

namespace UniCore.Api.Contracts.Users
{
    public sealed record AssignRoleRequest(string? Role, ScopeType? ScopeType, Guid? ScopeId);
}
