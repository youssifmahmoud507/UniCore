using UniCore.Application.Modules.Identity;

namespace UniCore.Application.Modules.Reporting
{
    public interface IReportScopeResolver
    {
        // From the scopes where the data sits (for example every scope of the workflow requests), the ones the
        // signed in user may see for this permission: the same rule as PermissionAuthorizer (a Faculty grant covers its
        // departments and sections), asked once per distinct scope instead of once per row.
        Task<ReportScope> ResolveAsync(string permission, IEnumerable<ScopeRef> dataScopes, CancellationToken cancellationToken = default);
    }




}
