using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    public sealed class UserManagementHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IIdentityAdministration identity, RolePermissionCatalog catalog, IRepository<ScopeAssignment> scopes, IRepository<RefreshToken> refreshTokens, IUnitOfWork unitOfWork, IClock clock, IEmailQueue emailQueue, IPasswordResetSettings settings, IAuditWriter audit)
    {
        private const int MaxSearchLength = 100;

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IIdentityAdministration _identity = identity;
        private readonly RolePermissionCatalog _catalog = catalog;
        private readonly IRepository<ScopeAssignment> _scopes = scopes;
        private readonly IRepository<RefreshToken> _refreshTokens = refreshTokens;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IEmailQueue _emailQueue = emailQueue;
        private readonly IPasswordResetSettings _settings = settings;
        private readonly IAuditWriter _audit = audit;

        private bool IsSuperAdmin => _user.IsInRole(RoleNames.SuperAdmin);

        private bool IsSelf(Guid userId) => _user.UserId == userId;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, null, cancellationToken);

        private Task AuditAsync(
            string action, Guid targetUserId, RequestInfo request, string? oldValues, string? newValues,
            CancellationToken cancellationToken)
            => _audit.WriteAsync(
                new AuditEntry(action, _user.UserId, "User", targetUserId.ToString(),
                    request.IpAddress, request.UserAgent, oldValues, newValues),
                cancellationToken);

        // ---------- queries ----------

        public async Task<Result<PagedResult<UserSummary>>> ListUsersAsync(PaginationQuery query, string? search, bool? isActive, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Users.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<UserSummary>>.Fail(auth.Errors);
            }

            var term = search?.Trim();
            if (term is { Length: > MaxSearchLength })
            {
                term = term[..MaxSearchLength];
            }

            return await _identity.ListUsersAsync(query, string.IsNullOrEmpty(term) ? null : term, isActive, cancellationToken);
        }

        public async Task<Result<UserDetail>> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Users.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UserDetail>.Fail(auth.Errors);
            }

            return await _identity.GetUserDetailAsync(userId, cancellationToken);
        }

        // ---------- users ----------

        public async Task<Result<UserSummary>> CreateUserAsync(string? userName, string? email, string? phoneNumber, RequestInfo request,CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Users.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UserSummary>.Fail(auth.Errors);
            }

            var created = await _identity.CreateUserAsync(userName, email, phoneNumber, _clock.UtcNow);
            if (!created.TryGetValue(out var user))
            {
                return Result<UserSummary>.Fail(created.Errors);
            }

            var queued = await _emailQueue.TryEnqueueAsync(AuthEmails.AccountCreated(user.Email, _settings.AppName, user.UserName), cancellationToken);
            await AuditAsync(AuditActions.UserCreated, user.Id, request, null, user.UserName, cancellationToken);

            if (!queued)
            {
                await AuditAsync(AuditActions.EmailEnqueueFailed, user.Id, request, null, null, cancellationToken);
            }

            return Result<UserSummary>.Ok(user);
        }

        public async Task<Result<UserSummary>> SetActiveAsync(Guid userId, bool active, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Users.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UserSummary>.Fail(auth.Errors);
            }

            if (IsSelf(userId))
            {
                return IdentityErrors.CannotModifyOwnAccess;
            }

            var found = await _identity.GetUserDetailAsync(userId, cancellationToken);
            if (!found.TryGetValue(out var detail))
            {
                return Result<UserSummary>.Fail(found.Errors);
            }

            if (detail.User.IsActive == active)
            {
                return Result<UserSummary>.Ok(detail.User);
            }

            if (detail.User.Roles.Contains(RoleNames.SuperAdmin))
            {
                if (!IsSuperAdmin)
                {
                    return IdentityErrors.OnlySuperAdminManagesSuperAdmin;
                }

                if (!active)
                {
                    var guard = await EnsureAnotherActiveSuperAdminAsync(userId, cancellationToken);
                    if (guard.IsFailure)
                    {
                        return Result<UserSummary>.Fail(guard.Errors);
                    }
                }
            }

            var now = _clock.UtcNow;

            var staged = await _identity.SetActiveAsync(userId, active, now, cancellationToken);
            if (staged.IsFailure)
            {
                return Result<UserSummary>.Fail(staged.Errors);
            }

            if (!active)
            {
                // A deactivated account loses its sessions at once.
                var sessions = await _refreshTokens.ListAsync(new ActiveRefreshTokensByUserSpecification(userId, now), cancellationToken);
                foreach (var session in sessions)
                {
                    session.Revoke(now, request.IpAddress); // cannot fail: the query only returns non-revoked tokens
                }
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<UserSummary>.Fail(saved.Errors);
            }

            await AuditAsync(
                active ? AuditActions.UserActivated : AuditActions.UserDeactivated,
                userId, request, (!active).ToString(), active.ToString(), cancellationToken);

            return Result<UserSummary>.Ok(detail.User with { IsActive = active });
        }

        // ---------- roles and scopes ----------

        // Assigns a role, optionally with a scope (a Dean of one faculty). Calling it again with
        // another scope adds that scope to the existing role assignment.
        public async Task<Result<UserDetail>> AssignRoleAsync(Guid userId,string? roleName,ScopeType? scopeType,Guid? scopeId,RequestInfo request,CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Roles.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UserDetail>.Fail(auth.Errors);
            }

            if (IsSelf(userId))
            {
                return IdentityErrors.CannotModifyOwnAccess;
            }

            var cleanName = roleName?.Trim();
            if (string.IsNullOrEmpty(cleanName))
            {
                return IdentityErrors.RoleNameRequired;
            }

            var roleResult = await _identity.FindRoleByNameAsync(cleanName, cancellationToken);
            if (!roleResult.TryGetValue(out var role))
            {
                return Result<UserDetail>.Fail(roleResult.Errors);
            }

            if (role.Name == RoleNames.SuperAdmin)
            {
                if (!IsSuperAdmin)
                {
                    return IdentityErrors.OnlySuperAdminManagesSuperAdmin;
                }

                // SuperAdmin is implicitly Global: a scope on it makes no sense.
                if (scopeType is not null)
                {
                    return IdentityErrors.ScopeInvalid;
                }
            }

            if (scopeType == ScopeType.Global && !IsSuperAdmin)
            {
                return IdentityErrors.CannotGrantGlobalScope;
            }

            // No privilege escalation: you can only hand out a role whose permissions you all hold.
            var codes = await _catalog.GetCodesAsync(role.Id, role.Name, cancellationToken);
            if (codes.Any(c => !_user.HasPermission(c)))
            {
                return IdentityErrors.CannotGrantPermission;
            }

            var now = _clock.UtcNow;

            ScopeAssignment? newScope = null;
            if (scopeType is { } type)
            {
                var created = ScopeAssignment.Create(role.Id, userId, type, scopeId, now);
                if (!created.TryGetValue(out newScope))
                {
                    return Result<UserDetail>.Fail(created.Errors);
                }
            }

            var added = await _identity.AddUserToRoleAsync(userId, role.Id, cancellationToken);
            if (!added.TryGetValue(out var roleAdded))
            {
                return Result<UserDetail>.Fail(added.Errors);
            }

            var scopeAdded = false;
            if (newScope is not null && scopeType is { } scopeKind)
            {
                var exists = await _scopes.AnyAsync(
                    new ScopeAssignmentMatchSpecification(userId, role.Id, scopeKind, scopeId), cancellationToken);

                if (!exists)
                {
                    _scopes.Add(newScope);
                    scopeAdded = true;
                }
            }

            if (roleAdded || scopeAdded)
            {
                var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (saved.IsFailure)
                {
                    return Result<UserDetail>.Fail(saved.Errors);
                }

                if (roleAdded)
                {
                    await AuditAsync(AuditActions.RoleAssigned, userId, request, null, role.Name, cancellationToken);
                }

                if (scopeAdded)
                {
                    await AuditAsync(AuditActions.ScopeAssigned, userId, request, null,
                        $"{role.Name}@{scopeType}:{scopeId}", cancellationToken);
                }
            }

            return await _identity.GetUserDetailAsync(userId, cancellationToken);
        }

        // Removes the role and every scope the user held for it.
        public async Task<Result<UserDetail>> RemoveRoleAsync(Guid userId, string? roleName, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Roles.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UserDetail>.Fail(auth.Errors);
            }

            if (IsSelf(userId))
            {
                return IdentityErrors.CannotModifyOwnAccess;
            }

            var cleanName = roleName?.Trim();
            if (string.IsNullOrEmpty(cleanName))
            {
                return IdentityErrors.RoleNameRequired;
            }

            var roleResult = await _identity.FindRoleByNameAsync(cleanName, cancellationToken);
            if (!roleResult.TryGetValue(out var role))
            {
                return Result<UserDetail>.Fail(roleResult.Errors);
            }

            var detailResult = await _identity.GetUserDetailAsync(userId, cancellationToken);
            if (!detailResult.TryGetValue(out var detail))
            {
                return detailResult;
            }

            if (role.Name == RoleNames.SuperAdmin)
            {
                if (!IsSuperAdmin)
                {
                    return IdentityErrors.OnlySuperAdminManagesSuperAdmin;
                }

                if (detail.User.IsActive && detail.User.Roles.Contains(RoleNames.SuperAdmin))
                {
                    var guard = await EnsureAnotherActiveSuperAdminAsync(userId, cancellationToken);
                    if (guard.IsFailure)
                    {
                        return Result<UserDetail>.Fail(guard.Errors);
                    }
                }
            }

            var removed = await _identity.RemoveUserFromRoleAsync(userId, role.Id, cancellationToken);
            if (!removed.TryGetValue(out var roleRemoved))
            {
                return Result<UserDetail>.Fail(removed.Errors);
            }

            if (!roleRemoved)
            {
                return Result<UserDetail>.Ok(detail); // nothing to do
            }

            var scopes = await _scopes.ListAsync(new ScopeAssignmentsByUserRoleSpecification(userId, role.Id), cancellationToken);
            foreach (var scope in scopes)
            {
                _scopes.Remove(scope);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<UserDetail>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.RoleRemoved, userId, request, role.Name, null, cancellationToken);

            foreach (var scope in scopes)
            {
                await AuditAsync(AuditActions.ScopeRemoved, userId, request,
                    $"{role.Name}@{scope.ScopeType}:{scope.ScopeId}", null, cancellationToken);
            }

            return await _identity.GetUserDetailAsync(userId, cancellationToken);
        }

        public async Task<Result<UserDetail>> RemoveScopeAsync(Guid userId, Guid scopeAssignmentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Roles.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UserDetail>.Fail(auth.Errors);
            }

            if (IsSelf(userId))
            {
                return IdentityErrors.CannotModifyOwnAccess;
            }

            var scope = await _scopes.FirstOrDefaultAsync(new ScopeAssignmentByIdSpecification(scopeAssignmentId), cancellationToken);
            if (scope is null || scope.UserId != userId)
            {
                return IdentityErrors.ScopeAssignmentNotFound;
            }

            _scopes.Remove(scope);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<UserDetail>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.ScopeRemoved, userId, request, $"{scope.ScopeType}:{scope.ScopeId}", null, cancellationToken);

            return await _identity.GetUserDetailAsync(userId, cancellationToken);
        }

        // Fails with LAST_SUPERADMIN when nobody else active holds the SuperAdmin role.
        private async Task<Result> EnsureAnotherActiveSuperAdminAsync(Guid excludingUserId, CancellationToken cancellationToken)
        {
            var superAdmin = await _identity.FindRoleByNameAsync(RoleNames.SuperAdmin, cancellationToken);
            if (!superAdmin.TryGetValue(out var role))
            {
                return Result.Fail(superAdmin.Errors);
            }

            var others = await _identity.CountActiveUsersInRoleAsync(role.Id, excludingUserId, cancellationToken);
            if (!others.TryGetValue(out var count))
            {
                return Result.Fail(others.Errors);
            }

            return count > 0 ? Result.Ok() : Result.Fail(IdentityErrors.LastSuperAdmin);
        }
    }



}
