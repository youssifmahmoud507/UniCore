using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class IdentityAdministration(AppDbContext db, UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles, ILogger<IdentityAdministration> logger) : IIdentityAdministration
    {
        private readonly AppDbContext _db = db;
        private readonly UserManager<ApplicationUser> _users = users;
        private readonly RoleManager<ApplicationRole> _roles = roles;
        private readonly ILogger<IdentityAdministration> _logger = logger;

        // ---------- roles ----------

        public Task<Result<IReadOnlyList<RoleSummary>>> ListRolesAsync(CancellationToken cancellationToken = default) => RunAsync(async () =>
            {
                var roles = await _db.Roles.AsNoTracking().Select(r => new { r.Id, r.Name }).ToListAsync(cancellationToken);

                var permissionCounts = (await _db.Set<RolePermission>().AsNoTracking()
                        .GroupBy(rp => rp.RoleId)
                        .Select(g => new { RoleId = g.Key, Count = g.Count() })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.RoleId, x => x.Count);

                var userCounts = (await _db.UserRoles.AsNoTracking()
                        .GroupBy(ur => ur.RoleId)
                        .Select(g => new { RoleId = g.Key, Count = g.Count() })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(x => x.RoleId, x => x.Count);

                var list = roles
                    .Select(r =>
                    {
                        var name = r.Name ?? string.Empty;
                        var permissions = name == RoleNames.SuperAdmin
                            ? Permissions.AllCodes.Count
                            : permissionCounts.GetValueOrDefault(r.Id);

                        return new RoleSummary(r.Id, name, permissions, userCounts.GetValueOrDefault(r.Id), RoleNames.All.Contains(name));
                    })
                    .OrderBy(r => r.Name, StringComparer.Ordinal)
                    .ToList();

                return Result<IReadOnlyList<RoleSummary>>.Ok(list);
            });

        public Task<Result<RoleBasic>> FindRoleAsync(Guid roleId, CancellationToken cancellationToken = default) => RunAsync<RoleBasic>(async () =>
            {
                var role = await _db.Roles.AsNoTracking()
                    .Where(r => r.Id == roleId)
                    .Select(r => new { r.Id, r.Name })
                    .FirstOrDefaultAsync(cancellationToken);

                if (role is null)
                {
                    return Result<RoleBasic>.Fail(IdentityErrors.RoleNotFound);
                }

                return Result<RoleBasic>.Ok(new RoleBasic(role.Id, role.Name ?? string.Empty));
            });

        public Task<Result<RoleBasic>> FindRoleByNameAsync(string name, CancellationToken cancellationToken = default)=> RunAsync(async () =>
            {
                var normalized = name.Trim().ToUpperInvariant();

                var role = await _db.Roles.AsNoTracking()
                    .Where(r => r.NormalizedName == normalized)
                    .Select(r => new { r.Id, r.Name })
                    .FirstOrDefaultAsync(cancellationToken);

                if (role is null)
                {
                    return Result<RoleBasic>.Fail(IdentityErrors.RoleNotFound);
                }

                return Result<RoleBasic>.Ok(new RoleBasic(role.Id, role.Name ?? string.Empty));
            });

        public Task<Result<int>> CountUsersInRoleAsync(Guid roleId, CancellationToken cancellationToken = default)=> RunAsync<int>(async () => Result<int>.Ok(await _db.UserRoles.CountAsync(ur => ur.RoleId == roleId, cancellationToken)));

        public Task<Result<int>> CountActiveUsersInRoleAsync(Guid roleId, Guid excludingUserId, CancellationToken cancellationToken = default)=> RunAsync(async () =>
            {
                var count = await (
                    from ur in _db.UserRoles
                    where ur.RoleId == roleId && ur.UserId != excludingUserId
                    join u in _db.Users on ur.UserId equals u.Id
                    where u.IsActive
                    select ur.UserId)
                    .CountAsync(cancellationToken);

                return Result<int>.Ok(count);
            });

        public Task<Result<RoleBasic>> CreateRoleAsync(string? name)=> RunAsync(async () =>
            {
                var created = ApplicationRole.Create(name);
                if (!created.TryGetValue(out var role))
                {
                    return Result<RoleBasic>.Fail(created.Errors);
                }

                var result = (await _roles.CreateAsync(role)).ToResult();
                if (result.IsFailure)
                {
                    return Result<RoleBasic>.Fail(result.Errors);
                }

                return Result<RoleBasic>.Ok(new RoleBasic(role.Id, role.Name ?? string.Empty));
            });

        public Task<Result> DeleteRoleAsync(Guid roleId)=> RunVoidAsync(async () =>
            {
                var role = await _roles.FindByIdAsync(roleId.ToString());
                if (role is null)
                {
                    return Result.Fail(IdentityErrors.RoleNotFound);
                }

                return (await _roles.DeleteAsync(role)).ToResult();
            });

        // ---------- users ----------

        public Task<Result<UserSummary>> CreateUserAsync(string? userName, string? email, string? phoneNumber, DateTimeOffset now) => RunAsync<UserSummary>(async () =>
            {
                var created = ApplicationUser.Create(userName, email, phoneNumber, now);
                if (!created.TryGetValue(out var user))
                {
                    return Result<UserSummary>.Fail(created.Errors);
                }

                // No password: the account owner sets it through the forgot-password flow.
                var result = (await _users.CreateAsync(user)).ToResult();
                if (result.IsFailure)
                {
                    return Result<UserSummary>.Fail(result.Errors);
                }

                return Result<UserSummary>.Ok(new UserSummary(
                    user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty,
                    user.IsActive, user.CreatedAt, null, Array.Empty<string>()));
            });

        public Task<Result<UserDetail>> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default) => RunAsync(async () =>
            {
                var user = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Id, u.UserName, u.Email, u.IsActive, u.CreatedAt, u.LastLoginAt })
                    .FirstOrDefaultAsync(cancellationToken);

                if (user is null)
                {
                    return Result<UserDetail>.Fail(IdentityErrors.UserNotFound);
                }

                var roleNames = await (
                    from ur in _db.UserRoles
                    where ur.UserId == userId
                    join r in _db.Roles on ur.RoleId equals r.Id
                    select r.Name)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var scopeRows = await (
                    from s in _db.Set<ScopeAssignment>()
                    where s.UserId == userId
                    join r in _db.Roles on s.RoleId equals r.Id
                    select new { s.Id, RoleName = r.Name, s.ScopeType, s.ScopeId, s.CreatedAt })
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var summary = new UserSummary(
                    user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty,
                    user.IsActive, user.CreatedAt, user.LastLoginAt,
                    [.. roleNames.OfType<string>().Order(StringComparer.Ordinal)]);

                var scopes = scopeRows
                    .Select(s => new ScopeAssignmentInfo(s.Id, s.RoleName ?? string.Empty, s.ScopeType, s.ScopeId, s.CreatedAt))
                    .OrderBy(s => s.Role, StringComparer.Ordinal)
                    .ToList();

                return Result<UserDetail>.Ok(new UserDetail(summary, scopes));
            });

        public Task<Result<PagedResult<UserSummary>>> ListUsersAsync(PaginationQuery query, string? search, bool? isActive, CancellationToken cancellationToken = default) => RunAsync(async () =>
            {
                var users = _db.Users.AsNoTracking();

                if (isActive is { } active)
                {
                    users = users.Where(u => u.IsActive == active);
                }

                if (!string.IsNullOrEmpty(search))
                {
                    // Identity keeps upper-cased copies of the name and email, so the search uses those.
                    var term = search.ToUpperInvariant();
                    users = users.Where(u =>
                        (u.NormalizedUserName != null && u.NormalizedUserName.Contains(term))
                        || (u.NormalizedEmail != null && u.NormalizedEmail.Contains(term)));
                }

                var total = await users.CountAsync(cancellationToken);

                var page = await users
                    .OrderBy(u => u.UserName)
                    .Skip(query.Skip)
                    .Take(query.NormalizedPageSize)
                    .Select(u => new { u.Id, u.UserName, u.Email, u.IsActive, u.CreatedAt, u.LastLoginAt })
                    .ToListAsync(cancellationToken);

                var ids = page.Select(u => u.Id).ToList();

                var roleRows = await (
                    from ur in _db.UserRoles
                    where ids.Contains(ur.UserId)
                    join r in _db.Roles on ur.RoleId equals r.Id
                    select new { ur.UserId, r.Name })
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var items = page
                    .Select(u => new UserSummary(
                        u.Id, u.UserName ?? string.Empty, u.Email ?? string.Empty, u.IsActive, u.CreatedAt, u.LastLoginAt,
                        [.. roleRows.Where(r => r.UserId == u.Id).Select(r => r.Name).OfType<string>().Order(StringComparer.Ordinal)]))
                    .ToList();

                return Result<PagedResult<UserSummary>>.Ok(PagedResult<UserSummary>.Create(items, query, total));
            });

        // ---------- staged changes (the handler saves) ----------

        public Task<Result> SetActiveAsync(Guid userId, bool active, DateTimeOffset now, CancellationToken cancellationToken = default) => RunVoidAsync(async () =>
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user is null)
                {
                    return Result.Fail(IdentityErrors.UserNotFound);
                }

                if (active)
                {
                    user.Activate(now);
                }
                else
                {
                    user.Deactivate(now);
                }

                return Result.Ok();
            });

        public Task<Result<bool>> AddUserToRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default) => RunAsync(async () =>
            {
                if (!await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
                {
                    return Result<bool>.Fail(IdentityErrors.UserNotFound);
                }

                if (await _db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken))
                {
                    return Result<bool>.Ok(false);
                }

                _db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
                return Result<bool>.Ok(true);
            });

        public Task<Result<bool>> RemoveUserFromRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default) => RunAsync(async () =>
            {
                var link = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

                if (link is null)
                {
                    return Result<bool>.Ok(false);
                }

                _db.UserRoles.Remove(link);
                return Result<bool>.Ok(true);
            });

        // ---------- boundary ----------

        private async Task<Result<T>> RunAsync<T>(Func<Task<Result<T>>> action)
        {
            try
            {
                return await action();
            }
            catch (OperationCanceledException)
            {
                return Result<T>.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError("Identity administration failed ({ExceptionType}).", ex.GetType().Name);
                return Result<T>.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }

        private async Task<Result> RunVoidAsync(Func<Task<Result>> action)
        {
            try
            {
                return await action();
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError("Identity administration failed ({ExceptionType}).", ex.GetType().Name);
                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }
    }




}
