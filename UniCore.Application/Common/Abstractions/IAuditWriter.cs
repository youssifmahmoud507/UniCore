namespace UniCore.Application.Common.Abstractions
{
    public interface IAuditWriter
    {
        // Never put passwords, tokens, OTPs or hashes in an entry.
        Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    }
}
