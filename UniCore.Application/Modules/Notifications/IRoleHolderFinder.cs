using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Notifications
{
    // The users that hold a role where the role covers the given scope (the "approvers of a step").
    public interface IRoleHolderFinder
    {
        Task<Result<IReadOnlyList<Guid>>> FindAsync(string role, ScopeType? scopeType, Guid? scopeId, CancellationToken cancellationToken = default);
    }


}
