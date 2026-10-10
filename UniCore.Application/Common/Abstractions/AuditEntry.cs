namespace UniCore.Application.Common.Abstractions
{
    public sealed record AuditEntry(string Action,Guid? ActorUserId = null,string? EntityName = null,string? EntityId = null,string? IpAddress = null,string? UserAgent = null);
}
