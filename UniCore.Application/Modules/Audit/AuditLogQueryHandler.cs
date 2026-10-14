using UniCore.Application.Common.Pagination;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;

namespace UniCore.Application.Modules.Audit
{
    // The API checks Audit.Read before calling this. The handler only validates the filter.
    public sealed class AuditLogQueryHandler(IAuditLogReader reader)
    {
        private readonly IAuditLogReader _reader = reader;

        public Task<Result<PagedResult<AuditLogItem>>> ListAsync(AuditLogFilter filter, PaginationQuery paging, CancellationToken cancellationToken = default)
        {
            if (filter.From is { } from && filter.To is { } to && from > to)
            {
                return Task.FromResult(Result<PagedResult<AuditLogItem>>.Fail(AuditErrors.FilterInvalid));
            }

            if (TooLong(filter.Action, AuditLog.MaxActionLength) || TooLong(filter.EntityName, AuditLog.MaxEntityNameLength) || TooLong(filter.EntityId, AuditLog.MaxEntityIdLength))
            {
                return Task.FromResult(Result<PagedResult<AuditLogItem>>.Fail(AuditErrors.FilterInvalid));
            }

            var clean = filter with
            {
                Action = Normalize(filter.Action)?.ToUpperInvariant(),
                EntityName = Normalize(filter.EntityName),
                EntityId = Normalize(filter.EntityId)
            };

            return _reader.ListAsync(clean, paging, cancellationToken);
        }

        public Task<Result<AuditLogItem>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
            {
                return Task.FromResult(Result<AuditLogItem>.Fail(AuditErrors.LogNotFound));
            }

            return _reader.GetAsync(id, cancellationToken);
        }

        private static bool TooLong(string? value, int max) => value is not null && value.Trim().Length > max;

        private static string? Normalize(string? value)
        {
            var clean = value?.Trim();
            return string.IsNullOrEmpty(clean) ? null : clean;
        }
    }



}
