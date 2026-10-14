using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeScopeHierarchy : IScopeHierarchy
    {
        public Dictionary<(ScopeType Type, Guid Id), IReadOnlyList<ScopeRef>> Chains { get; } = [];
        public Task<IReadOnlyList<ScopeRef>> GetChainAsync(ScopeType type, Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Chains.TryGetValue((type, id), out var chain) ? chain : [new ScopeRef(type, id)]);
    }

}
