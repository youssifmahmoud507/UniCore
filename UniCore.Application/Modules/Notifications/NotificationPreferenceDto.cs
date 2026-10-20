namespace UniCore.Application.Modules.Notifications
{
    public sealed record NotificationPreferenceDto(string Type, string Name, bool Mandatory, bool InAppEnabled, bool EmailEnabled);


}
