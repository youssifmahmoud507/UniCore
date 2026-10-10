using UniCore.Application.Common.Abstractions;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = new();

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
