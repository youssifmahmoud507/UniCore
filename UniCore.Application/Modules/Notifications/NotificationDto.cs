namespace UniCore.Application.Modules.Notifications
{
    public sealed record NotificationDto(Guid Id, string Type, string Title, string Content, string? ReferenceType, Guid? ReferenceId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, bool IsRead);


}
