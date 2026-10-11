using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class RoleManagementHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IIdentityAdministration identity, IRepository<Permission> permissions, IRepository<RolePermission> rolePermissions, RolePermissionCatalog catalog, IUnitOfWork unitOfWork, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IIdentityAdministration _identity = identity;
        private readonly IRepository<Permission> _permissions = permissions;
        private readonly IRepository<RolePermission> _rolePermissions = rolePermissions;
        private readonly RolePermissionCatalog _catalog = catalog;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<IReadOnlyList<PermissionInfo>>> ListPermissionsAsync(CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Roles.Manage, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<PermissionInfo>>.Fail(auth.Errors);
            }

            var all = await _permissions.ListAsync(new AllPermissionsSpecification(), cancellationToken);

            var list = all.Select(p => new PermissionInfo(p.Code, p.Module, p.Description ?? string.Empty)).ToList();

            return Result<IReadOnlyList<PermissionInfo>>.Ok(list);
        }

        public async Task<Result<IReadOnlyList<RoleSummary>>> ListRolesAsync(CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Roles.Manage, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<RoleSummary>>.Fail(auth.Errors);
            }

            return await _identity.ListRolesAsync(cancellationToken);
        }

        public async Task<Result<RoleDetail>> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Roles.Manage, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<RoleDetail>.Fail(auth.Errors);
            }

            var found = await _identity.FindRoleAsync(roleId, cancellationToken);
            if (!found.TryGetValue(out var role))
            {
                return Result<RoleDetail>.Fail(found.Errors);
            }

            return Result<RoleDetail>.Ok(await BuildDetailAsync(role, cancellationToken));
        }

        public async Task<Result<RoleDetail>> CreateRoleAsync(string? name, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Roles.Manage, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<RoleDetail>.Fail(auth.Errors);
            }

            var created = await _identity.CreateRoleAsync(name);
            if (!created.TryGetValue(out var role))
            {
                return Result<RoleDetail>.Fail(created.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.RoleCreated, _user.UserId, "Role", role.Id.ToString(),
                    request.IpAddress, request.UserAgent, NewValues: role.Name),
                cancellationToken);

            return Result<RoleDetail>.Ok(new RoleDetail(role.Id, role.Name, false, Array.Empty<string>()));
        }

        public async Task<Result> DeleteRoleAsync(Guid roleId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Roles.Manage, null, cancellationToken);
            if (auth.IsFailure)
            {
                return auth;
            }

            var found = await _identity.FindRoleAsync(roleId, cancellationToken);
            if (!found.TryGetValue(out var role))
            {
                return Result.Fail(found.Errors);
            }

            if (RoleNames.All.Contains(role.Name))
            {
                return Result.Fail(IdentityErrors.RoleProtected);
            }

            var users = await _identity.CountUsersInRoleAsync(roleId, cancellationToken);
            if (!users.TryGetValue(out var userCount))
            {
                return Result.Fail(users.Errors);
            }

            if (userCount > 0)
            {
                return Result.Fail(IdentityErrors.RoleInUse);
            }

            // Stage the removal of the role's permissions: they are saved together with the role.
            var links = await _rolePermissions.ListAsync(new RolePermissionsByRoleSpecification(roleId), cancellationToken);
            foreach (var link in links)
            {
                _rolePermissions.Remove(link);
            }

            var deleted = await _identity.DeleteRoleAsync(roleId);
            if (deleted.IsFailure)
            {
                return deleted;
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.RoleDeleted, _user.UserId, "Role", roleId.ToString(),
                    request.IpAddress, request.UserAgent, OldValues: role.Name),
                cancellationToken);

            return Result.Ok();
        }

        // Replaces the permission set of a role.
        public async Task<Result<RoleDetail>> SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string>? codes, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.PermissionAdmin.Manage, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<RoleDetail>.Fail(auth.Errors);
            }

            if (codes is null)
            {
                return IdentityErrors.PermissionsRequired;
            }

            var found = await _identity.FindRoleAsync(roleId, cancellationToken);
            if (!found.TryGetValue(out var role))
            {
                return Result<RoleDetail>.Fail(found.Errors);
            }

            if (role.Name == RoleNames.SuperAdmin)
            {
                return IdentityErrors.RoleImmutable;
            }

            var requested = codes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var all = await _permissions.ListAsync(new AllPermissionsSpecification(), cancellationToken);
            var byCode = all.ToDictionary(p => p.Code, StringComparer.Ordinal);

            var unknown = requested.Where(c => !byCode.ContainsKey(c)).ToList();
            if (unknown.Count > 0)
            {
                return IdentityErrors.PermissionNotFound(unknown);
            }

            var codeById = all.ToDictionary(p => p.Id, p => p.Code);
            var current = await _rolePermissions.ListAsync(new RolePermissionsByRoleSpecification(roleId), cancellationToken);

            var currentCodes = current
                .Select(rp => codeById.TryGetValue(rp.PermissionId, out var code) ? code : null)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            var requestedSet = requested.ToHashSet(StringComparer.Ordinal);

            var toAdd = requested.Where(c => !currentCodes.Contains(c)).ToList();

            var toRemove = current
                .Where(rp => codeById.TryGetValue(rp.PermissionId, out var code) && !requestedSet.Contains(code))
                .ToList();

            if (toAdd.Count == 0 && toRemove.Count == 0)
            {
                return Result<RoleDetail>.Ok(await BuildDetailAsync(role, cancellationToken));
            }

            // No privilege escalation: you can only hand out permissions you hold yourself.
            if (toAdd.Any(c => !_user.HasPermission(c)))
            {
                return IdentityErrors.CannotGrantPermission;
            }

            foreach (var code in toAdd)
            {
                if (!byCode.TryGetValue(code, out var permission))
                {
                    continue;
                }

                var link = RolePermission.Create(role.Id, permission.Id);
                if (!link.TryGetValue(out var rolePermission))
                {
                    return Result<RoleDetail>.Fail(link.Errors);
                }

                _rolePermissions.Add(rolePermission);
            }

            var removedCodes = toRemove
                .Select(rp => codeById.TryGetValue(rp.PermissionId, out var code) ? code : null)
                .OfType<string>()
                .ToList();

            foreach (var row in toRemove)
            {
                _rolePermissions.Remove(row);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<RoleDetail>.Fail(saved.Errors);
            }

            if (toAdd.Count > 0)
            {
                await _audit.WriteAsync(
                    new AuditEntry(AuditActions.PermissionGranted, _user.UserId, "Role", role.Id.ToString(),
                        request.IpAddress, request.UserAgent, NewValues: string.Join(",", toAdd)),
                    cancellationToken);
            }

            if (removedCodes.Count > 0)
            {
                await _audit.WriteAsync(
                    new AuditEntry(AuditActions.PermissionRevoked, _user.UserId, "Role", role.Id.ToString(),
                        request.IpAddress, request.UserAgent, OldValues: string.Join(",", removedCodes)),
                    cancellationToken);
            }

            return Result<RoleDetail>.Ok(new RoleDetail(role.Id, role.Name, RoleNames.All.Contains(role.Name), [.. requested.Order(StringComparer.Ordinal)]));
        }

        private async Task<RoleDetail> BuildDetailAsync(RoleBasic role, CancellationToken cancellationToken)
        {
            var codes = await _catalog.GetCodesAsync(role.Id, role.Name, cancellationToken);

            return new RoleDetail(role.Id, role.Name, RoleNames.All.Contains(role.Name), codes);
        }
    }



}
