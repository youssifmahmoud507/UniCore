using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class PermissionAuthorizer(ICurrentUser user, IUserAccessContext access, IScopeHierarchy hierarchy) : IPermissionAuthorizer
    {
        private readonly ICurrentUser _user = user;
        private readonly IUserAccessContext _access = access;
        private readonly IScopeHierarchy _hierarchy = hierarchy;

        public async Task<Result> AuthorizeAsync(string permission, ResourceContext? resource = null, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Result.Fail(Error.Unauthorized());
            }

            // Only the roles that actually grant this permission can open a scope.
            var grants = _access.Current?.Grants.Where(g => g.Permissions.Contains(permission)).ToList();
            if (grants is null || grants.Count == 0)
            {
                return Result.Fail(IdentityErrors.ForbiddenPermissionMissing);
            }

            if (resource is null)
            {
                return Result.Ok();
            }

            // Ownership: the student of the record, the instructors assigned to the section...
            if (resource.OwnerUserIds is { Count: > 0 } owners && owners.Contains(userId))
            {
                return Result.Ok();
            }

            var scopes = grants.SelectMany(g => g.Scopes).ToList();

            if (scopes.Any(s => s.ScopeType == ScopeType.Global))
            {
                return Result.Ok();
            }

            if (resource.ScopeType is { } type && type != ScopeType.Global && resource.ScopeId is { } id)
            {
                var chain = await _hierarchy.GetChainAsync(type, id, cancellationToken);

                var covered = scopes.Any(s => s.ScopeId is { } scopeId && chain.Any(c => c.Type == s.ScopeType && c.Id == scopeId));

                return covered ? Result.Ok() : Result.Fail(IdentityErrors.ForbiddenOutOfScope);
            }

            // No usable scope information on the resource: fail closed.
            return Result.Fail(resource.OwnerUserIds is { Count: > 0 } ? IdentityErrors.ForbiddenNotOwner : IdentityErrors.ForbiddenOutOfScope);
        }
    }



}
