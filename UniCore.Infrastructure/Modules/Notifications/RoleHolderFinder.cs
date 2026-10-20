using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Notifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Notifications
{
    // The same rule as WorkflowApproverCheck, seen from the other side: who can act on a step?
    // A user qualifies when the account is active, the role carries Requests.Process, and one of the user's
    // scopes for that role is Global or sits in the chain of the request's scope.
    public sealed class RoleHolderFinder(AppDbContext db, IScopeHierarchy hierarchy, ILogger<RoleHolderFinder> logger) : IRoleHolderFinder
    {
        private readonly AppDbContext _db = db;
        private readonly IScopeHierarchy _hierarchy = hierarchy;
        private readonly ILogger<RoleHolderFinder> _logger = logger;

        public async Task<Result<IReadOnlyList<Guid>>> FindAsync(string role, ScopeType? scopeType, Guid? scopeId, CancellationToken cancellationToken = default)
        {
            try
            {
                var roleRow = await _db.Roles.AsNoTracking().Where(r => r.Name == role).Select(r => new { r.Id }).FirstOrDefaultAsync(cancellationToken);
                if (roleRow is null)
                {
                    return Result<IReadOnlyList<Guid>>.Ok([]);
                }

                var canProcess = await ( from rp in _db.Set<RolePermission>() join p in _db.Set<Permission>() on rp.PermissionId equals p.Id where rp.RoleId == roleRow.Id && p.Code == Permissions.Requests.Process select rp.RoleId).AnyAsync(cancellationToken);

                if (!canProcess)
                {
                    return Result<IReadOnlyList<Guid>>.Ok([]);
                }

                var holders = await ( from ur in _db.UserRoles join u in _db.Users on ur.UserId equals u.Id where ur.RoleId == roleRow.Id && u.IsActive select ur.UserId).Distinct().ToListAsync(cancellationToken);

                if (holders.Count == 0)
                {
                    return Result<IReadOnlyList<Guid>>.Ok([]);
                }

                var scopes = await _db.Set<ScopeAssignment>().AsNoTracking().Where(s => s.RoleId == roleRow.Id && s.UserId != null && holders.Contains(s.UserId.Value)).Select(s => new { UserId = s.UserId!.Value, s.ScopeType, s.ScopeId }).ToListAsync(cancellationToken);

                var chain = new List<ScopeRef>();

                if (scopeType is { } type && scopeId is { } id)
                {
                    chain.AddRange(await _hierarchy.GetChainAsync(type, id, cancellationToken));
                }

                var users = scopes.Where(s => s.ScopeType == ScopeType.Global || (s.ScopeId is { } scopeValue && chain.Any(c => c.Type == s.ScopeType && c.Id == scopeValue))).Select(s => s.UserId).Distinct().ToList();

                return Result<IReadOnlyList<Guid>>.Ok(users);
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex)
            {
                _logger.LogError("Finding the holders of a role failed ({ExceptionType}).", ex.GetType().Name);
                return CommonErrors.ExternalServiceUnavailable;
            }
        }
    }

}
