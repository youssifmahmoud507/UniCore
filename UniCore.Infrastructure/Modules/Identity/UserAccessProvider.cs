using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class UserAccessProvider(AppDbContext db, ILogger<UserAccessProvider> logger) : IUserAccessProvider
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<UserAccessProvider> _logger = logger;

        public async Task<Result<UserAccess>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                // A deactivated account loses every permission immediately, even with a valid token.
                var active = await _db.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken);

                if (!active)
                {
                    return new UserAccess(userId, Array.Empty<RoleGrant>());
                }

                var roles = await (
                    from userRole in _db.UserRoles
                    where userRole.UserId == userId
                    join role in _db.Roles on userRole.RoleId equals role.Id
                    select new { role.Id, role.Name })
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                if (roles.Count == 0)
                {
                    return new UserAccess(userId, Array.Empty<RoleGrant>());
                }

                var roleIds = roles.Select(r => r.Id).ToList();

                var permissionRows = await (
                    from rp in _db.Set<RolePermission>()
                    where roleIds.Contains(rp.RoleId)
                    join p in _db.Set<Permission>() on rp.PermissionId equals p.Id
                    select new { rp.RoleId, p.Code }).AsNoTracking().ToListAsync(cancellationToken);

                var scopeRows = await _db.Set<ScopeAssignment>().AsNoTracking().Where(s => s.UserId == userId).Select(s => new { s.RoleId, s.ScopeType, s.ScopeId }).ToListAsync(cancellationToken);

                var grants = new List<RoleGrant>();

                foreach (var role in roles)
                {
                    var name = role.Name ?? string.Empty;

                    var scopes = scopeRows.Where(s => s.RoleId == role.Id).Select(s => new ScopeGrant(s.ScopeType, s.ScopeId)).ToList();

                    HashSet<string> permissions;

                    if (name == RoleNames.SuperAdmin)
                    {
                        // SuperAdmin: every permission, Global scope, nothing to configure or to lose.
                        permissions = new HashSet<string>(Permissions.AllCodes, StringComparer.Ordinal);
                        scopes.Add(new ScopeGrant(ScopeType.Global, null));
                    }
                    else
                    {
                        permissions = permissionRows.Where(p => p.RoleId == role.Id).Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
                    }

                    grants.Add(new RoleGrant(name, permissions, scopes.Distinct().ToList()));
                }

                return new UserAccess(userId, grants);
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex)
            {
                _logger.LogError("Loading user access failed ({ExceptionType}).", ex.GetType().Name);
                return CommonErrors.ExternalServiceUnavailable;
            }
        }
    }




}
