namespace UniCore.Api.Contracts.Notifications
{
    public sealed record SetNotificationPreferenceRequest(bool InAppEnabled, bool EmailEnabled);

}
