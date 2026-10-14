using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Audit;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Audit
{
    public sealed class AuditLogReader(AppDbContext db, ILogger<AuditLogReader> logger) : IAuditLogReader
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<AuditLogReader> _logger = logger;

        public async Task<Result<PagedResult<AuditLogItem>>> ListAsync(AuditLogFilter filter, PaginationQuery paging, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = Apply(_db.Set<AuditLog>().AsNoTracking(), filter);

                var total = await query.CountAsync(cancellationToken);

                var items = await query.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Skip(paging.Skip).Take(paging.NormalizedPageSize).Select(a => new AuditLogItem(a.Id, a.Action, a.ActorUserId, a.EntityName, a.EntityId, a.IpAddress, a.UserAgent, a.OldValues, a.NewValues, a.CorrelationId, a.CreatedAt)).ToListAsync(cancellationToken);

                return PagedResult<AuditLogItem>.Create(items, paging, total);
            }
            catch (OperationCanceledException)
            {
                return Result<PagedResult<AuditLogItem>>.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Audit log list failed ({ExceptionType}).", ex.GetType().Name);
                return Result<PagedResult<AuditLogItem>>.Fail(AuditErrors.StorageUnavailable);
            }
        }

        public async Task<Result<AuditLogItem>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var item = await _db.Set<AuditLog>().AsNoTracking().Where(a => a.Id == id).Select(a => new AuditLogItem(a.Id, a.Action, a.ActorUserId, a.EntityName, a.EntityId, a.IpAddress, a.UserAgent, a.OldValues, a.NewValues, a.CorrelationId, a.CreatedAt)).FirstOrDefaultAsync(cancellationToken);

                return item is null ? Result<AuditLogItem>.Fail(AuditErrors.LogNotFound) : Result<AuditLogItem>.Ok(item);
            }
            catch (OperationCanceledException)
            {
                return Result<AuditLogItem>.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Audit log read failed ({ExceptionType}).", ex.GetType().Name);
                return Result<AuditLogItem>.Fail(AuditErrors.StorageUnavailable);
            }
        }

        public async Task<Result<bool>> ExistsAsync(string action, string entityId, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await _db.Set<AuditLog>().AsNoTracking()
                    .AnyAsync(a => a.Action == action && a.EntityId == entityId, cancellationToken);

                return exists;
            }
            catch (OperationCanceledException)
            {
                return Result<bool>.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Audit log existence check failed ({ExceptionType}).", ex.GetType().Name);
                return Result<bool>.Fail(AuditErrors.StorageUnavailable);
            }
        }

        private static IQueryable<AuditLog> Apply(IQueryable<AuditLog> query, AuditLogFilter filter)
        {
            if (filter.Action is { } action)
            {
                query = query.Where(a => a.Action == action);
            }

            if (filter.ActorUserId is { } actor)
            {
                query = query.Where(a => a.ActorUserId == actor);
            }

            if (filter.EntityName is { } entityName)
            {
                query = query.Where(a => a.EntityName == entityName);
            }

            if (filter.EntityId is { } entityId)
            {
                query = query.Where(a => a.EntityId == entityId);
            }

            if (filter.From is { } from)
            {
                query = query.Where(a => a.CreatedAt >= from);
            }

            if (filter.To is { } to)
            {
                query = query.Where(a => a.CreatedAt <= to);
            }

            return query;
        }
    }


}
