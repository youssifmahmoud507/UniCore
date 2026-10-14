using UniCore.Application.Common.Pagination;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Audit
{
    public interface IAuditLogReader
    {
        Task<Result<PagedResult<AuditLogItem>>> ListAsync(AuditLogFilter filter, PaginationQuery paging, CancellationToken cancellationToken = default);

        Task<Result<AuditLogItem>> GetAsync(Guid id, CancellationToken cancellationToken = default);

        // Used by event consumers to stay idempotent.
        Task<Result<bool>> ExistsAsync(string action, string entityId, CancellationToken cancellationToken = default);
    }



}
