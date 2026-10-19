using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Workflow
{
    // Who may act on a step: the user holds the step's role (with Requests.Process), and that role's scope covers the request.
    // SuperAdmin may act on any step. A request without a scope can be acted on only by roles with a Global scope (fail closed).
    public sealed class WorkflowApproverCheck(IUserAccessContext access, IScopeHierarchy hierarchy)
    {
        private readonly IUserAccessContext _access = access;
        private readonly IScopeHierarchy _hierarchy = hierarchy;

        // The roles whose steps this user could act on. Used for the inbox.
        public IReadOnlyList<string> ApproverRoles()
        {
            var grants = _access.Current?.Grants;

            if (grants is null)
            {
                return [];
            }

            if (grants.Any(g => g.Role == RoleNames.SuperAdmin))
            {
                return RoleNames.All;
            }

            return [.. grants.Where(g => g.Permissions.Contains(Permissions.Requests.Process)).Select(g => g.Role).Distinct()];
        }

        public async Task<bool> CanActAsync(string role, ScopeType? scopeType, Guid? scopeId, CancellationToken cancellationToken = default)
        {
            var grants = _access.Current?.Grants;

            if (grants is null)
            {
                return false;
            }

            if (grants.Any(g => g.Role == RoleNames.SuperAdmin))
            {
                return true;
            }

            var matching = grants.Where(g => g.Role == role && g.Permissions.Contains(Permissions.Requests.Process)).ToList();

            if (matching.Count == 0)
            {
                return false;
            }

            var scopes = matching.SelectMany(g => g.Scopes).ToList();

            if (scopes.Any(s => s.ScopeType == ScopeType.Global))
            {
                return true;
            }

            if (scopeType is not { } type || scopeId is not { } id)
            {
                return false;
            }

            var chain = await _hierarchy.GetChainAsync(type, id, cancellationToken);

            return scopes.Any(s => s.ScopeId is { } scopeValue && chain.Any(c => c.Type == s.ScopeType && c.Id == scopeValue));
        }
    }



}
