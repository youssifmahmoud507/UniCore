using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting
{
    public sealed class ReportScopeResolver(IUserAccessContext access, IScopeHierarchy hierarchy) : IReportScopeResolver
    {
        private readonly IUserAccessContext _access = access;
        private readonly IScopeHierarchy _hierarchy = hierarchy;

        public async Task<ReportScope> ResolveAsync(string permission, IEnumerable<ScopeRef> dataScopes, CancellationToken cancellationToken = default)
        {
            var scopes = _access.Current?.Grants.Where(g => g.Permissions.Contains(permission)).SelectMany(g => g.Scopes).ToList() ?? [];

            if (scopes.Count == 0)
            {
                return ReportScope.Nothing;
            }

            if (scopes.Any(s => s.ScopeType == ScopeType.Global))
            {
                return ReportScope.Everything;
            }

            var allowed = new HashSet<Guid>();

            foreach (var dataScope in dataScopes.Where(s => s.Type != ScopeType.Global).Distinct())
            {
                var chain = await _hierarchy.GetChainAsync(dataScope.Type, dataScope.Id, cancellationToken);

                if (scopes.Any(s => s.ScopeId is { } scopeId && chain.Any(c => c.Type == s.ScopeType && c.Id == scopeId)))
                {
                    allowed.Add(dataScope.Id);
                }
            }

            return new ReportScope(false, allowed);
        }
    }




}
