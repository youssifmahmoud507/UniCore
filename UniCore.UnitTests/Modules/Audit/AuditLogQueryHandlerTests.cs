using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Audit;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;

namespace UniCore.UnitTests.Modules.Audit
{
    public class AuditLogQueryHandlerTests
    {
        private sealed class CapturingReader : IAuditLogReader
        {
            public AuditLogFilter? LastFilter { get; private set; }

            public Task<Result<PagedResult<AuditLogItem>>> ListAsync(AuditLogFilter filter, PaginationQuery paging, CancellationToken cancellationToken = default)
            {
                LastFilter = filter;
                return Task.FromResult(Result<PagedResult<AuditLogItem>>.Ok(PagedResult<AuditLogItem>.Create([], paging, 0)));
            }
            public Task<Result<AuditLogItem>> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditLogItem>.Fail(AuditErrors.LogNotFound));
            public Task<Result<bool>> ExistsAsync(string action, string entityId, CancellationToken cancellationToken = default) => Task.FromResult(Result<bool>.Ok(false));
        }

        private readonly CapturingReader _reader = new();
        private readonly AuditLogQueryHandler _handler;

        public AuditLogQueryHandlerTests() => _handler = new AuditLogQueryHandler(_reader);

        [Fact]
        public async Task A_start_after_the_end_is_rejected_before_reading()
        {
            var filter = new AuditLogFilter(From: new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero), To: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

            var result = await _handler.ListAsync(filter, new PaginationQuery());

            Assert.True(result.IsFailure);
            Assert.Equal("AUDIT_FILTER_INVALID", result.Errors[0].Code);
            Assert.Null(_reader.LastFilter);
        }

        [Fact]
        public async Task Text_filters_longer_than_their_column_are_rejected()
        {
            var result = await _handler.ListAsync(new AuditLogFilter(EntityId: new string('1', AuditLog.MaxEntityIdLength + 1)), new PaginationQuery());

            Assert.True(result.IsFailure);
            Assert.Equal("AUDIT_FILTER_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public async Task Filters_are_trimmed_and_the_action_is_upper_cased()
        {
            var result = await _handler.ListAsync(new AuditLogFilter(Action: "  user_created ", EntityName: " User ", EntityId: "   "), new PaginationQuery());

            Assert.True(result.IsSuccess);
            Assert.NotNull(_reader.LastFilter);
            Assert.Equal("USER_CREATED", _reader.LastFilter.Action);
            Assert.Equal("User", _reader.LastFilter.EntityName);
            Assert.Null(_reader.LastFilter.EntityId);
        }

        [Fact]
        public async Task An_empty_id_is_not_found_without_reading()
        {
            var result = await _handler.GetAsync(Guid.Empty);

            Assert.True(result.IsFailure);
            Assert.Equal("AUDIT_LOG_NOT_FOUND", result.Errors[0].Code);
        }
    }



}
