namespace UniCore.Application.Modules.Notifications
{
    // SourceEventId: the domain event that caused it. With the user and the type it makes a retry safe (no second notification).
    public sealed record NotificationRequest(Guid UserId, string Type, string Title, string Content, string? ReferenceType = null, Guid? ReferenceId = null, Guid? SourceEventId = null);


}
