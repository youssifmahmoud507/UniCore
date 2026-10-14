namespace UniCore.Application.Modules.Audit
{
    public sealed record AuditLogItem(Guid Id,string Action,Guid? ActorUserId,string? EntityName,string? EntityId,string? IpAddress,string? UserAgent,string? OldValues,string? NewValues,string? CorrelationId,DateTimeOffset CreatedAt);



}
