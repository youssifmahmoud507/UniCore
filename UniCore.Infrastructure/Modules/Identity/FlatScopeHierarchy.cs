using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    // Temporary: no parent/child knowledge, so a Dean at a faculty matches that faculty only.
    // Developer 1 replaces this with the real Section -> Department -> Faculty hierarchy.
    public sealed class FlatScopeHierarchy : IScopeHierarchy
    {
        public Task<IReadOnlyList<ScopeRef>> GetChainAsync(ScopeType type, Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScopeRef>>([new ScopeRef(type, id)]);
    }




}
