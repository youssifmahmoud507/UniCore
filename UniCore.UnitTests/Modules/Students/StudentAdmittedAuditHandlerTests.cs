using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Audit;
using UniCore.Application.Modules.Students;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Students;

namespace UniCore.UnitTests.Modules.Students
{
    public class StudentAdmittedAuditHandlerTests
    {
        private sealed class RecordingWriter : IAuditWriter
        {
            public List<AuditEntry> Entries { get; } = [];

            public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
            {
                Entries.Add(entry);
                return Task.CompletedTask;
            }
        }

        private sealed class StubReader(Result<bool> exists) : IAuditLogReader
        {
            public Task<Result<PagedResult<AuditLogItem>>> ListAsync(AuditLogFilter filter, PaginationQuery paging, CancellationToken cancellationToken = default)
                => Task.FromResult(Result<PagedResult<AuditLogItem>>.Ok(PagedResult<AuditLogItem>.Create([], paging, 0)));

            public Task<Result<AuditLogItem>> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult(Result<AuditLogItem>.Fail(AuditErrors.LogNotFound));

            public Task<Result<bool>> ExistsAsync(string action, string entityId, CancellationToken cancellationToken = default)
                => Task.FromResult(exists);
        }

        private static StudentAdmitted NewEvent() => new(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "2026CS0001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1));

        [Fact]
        public async Task The_first_delivery_writes_one_audit_row_without_an_actor()
        {
            var writer = new RecordingWriter();
            var handler = new StudentAdmittedAuditHandler(writer, new StubReader(Result<bool>.Ok(false)));
            var admitted = NewEvent();

            var result = await handler.HandleAsync(admitted);

            Assert.True(result.IsSuccess);
            var entry = Assert.Single(writer.Entries);
            Assert.Equal(AuditActions.StudentAdmitted, entry.Action);
            Assert.Null(entry.ActorUserId);
            Assert.Equal("Student", entry.EntityName);
            Assert.Equal(admitted.StudentId.ToString(), entry.EntityId);
            Assert.Equal("2026CS0001", entry.NewValues);
        }

        [Fact]
        public async Task A_repeated_delivery_writes_nothing()
        {
            var writer = new RecordingWriter();
            var handler = new StudentAdmittedAuditHandler(writer, new StubReader(Result<bool>.Ok(true)));

            var result = await handler.HandleAsync(NewEvent());

            Assert.True(result.IsSuccess);
            Assert.Empty(writer.Entries);
        }

        [Fact]
        public async Task A_storage_failure_is_returned_so_the_outbox_retries()
        {
            var writer = new RecordingWriter();
            var handler = new StudentAdmittedAuditHandler(writer, new StubReader(Result<bool>.Fail(AuditErrors.StorageUnavailable)));

            var result = await handler.HandleAsync(NewEvent());

            Assert.True(result.IsFailure);
            Assert.Equal("AUDIT_STORAGE_UNAVAILABLE", result.Errors[0].Code);
            Assert.Empty(writer.Entries);
        }
    }

}
