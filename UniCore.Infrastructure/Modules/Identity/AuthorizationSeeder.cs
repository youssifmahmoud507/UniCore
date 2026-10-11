using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class AuthorizationSeeder(AppDbContext db, ILogger<AuthorizationSeeder> logger)
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<AuthorizationSeeder> _logger = logger;

        // Idempotent. Missing permissions are added. Default role permissions are seeded only for
        // roles that have none yet, so manual changes are never overwritten by a restart.
        public async Task<Result> SeedAsync()
        {
            try
            {
                var existing = (await _db.Set<Permission>().AsNoTracking().Select(p => p.Code).ToListAsync()).ToHashSet(StringComparer.Ordinal);

                var addedPermissions = 0;

                foreach (var definition in Permissions.All)
                {
                    if (existing.Contains(definition.Code))
                    {
                        continue;
                    }

                    var created = Permission.Create(definition.Code, definition.Code, definition.Description, definition.Module);
                    if (!created.TryGetValue(out var permission))
                    {
                        return Result.Fail(created.Errors);
                    }

                    _db.Set<Permission>().Add(permission);
                    addedPermissions++;
                }

                if (addedPermissions > 0)
                {
                    await _db.SaveChangesAsync();
                }

                var permissionIds = await _db.Set<Permission>().AsNoTracking().ToDictionaryAsync(p => p.Code, p => p.Id);

                var roles = await _db.Roles.AsNoTracking().ToListAsync();

                var rolesWithPermissions = (await _db.Set<RolePermission>().AsNoTracking().Select(rp => rp.RoleId).Distinct().ToListAsync()).ToHashSet();

                var addedLinks = 0;

                foreach (var role in roles)
                {
                    if (role.Name is null || rolesWithPermissions.Contains(role.Id) || !DefaultRolePermissions.ByRole.TryGetValue(role.Name, out var codes))
                    {
                        continue;
                    }

                    foreach (var code in codes)
                    {
                        if (!permissionIds.TryGetValue(code, out var permissionId))
                        {
                            continue;
                        }

                        var link = RolePermission.Create(role.Id, permissionId);
                        if (!link.TryGetValue(out var rolePermission))
                        {
                            return Result.Fail(link.Errors);
                        }

                        _db.Set<RolePermission>().Add(rolePermission);
                        addedLinks++;
                    }
                }

                if (addedLinks > 0)
                {
                    await _db.SaveChangesAsync();
                }

                _logger.LogInformation("Authorization seeding finished. Added {Permissions} permission(s) and {Links} role permission(s).", addedPermissions, addedLinks);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError("Authorization seeding could not run ({ExceptionType}). Is the database migrated?", ex.GetType().Name);
                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }
    }




}
