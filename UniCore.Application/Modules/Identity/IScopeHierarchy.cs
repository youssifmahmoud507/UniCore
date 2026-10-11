using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    // Contract implemented by Developer 1 (the academic structure owner).
    public interface IScopeHierarchy
    {
        // The resource itself plus every scope that contains it, for example
        // Section -> [Section, Department, Faculty]. Global is implied and not listed.
        Task<IReadOnlyList<ScopeRef>> GetChainAsync(ScopeType type, Guid id, CancellationToken cancellationToken = default);
    }



}
