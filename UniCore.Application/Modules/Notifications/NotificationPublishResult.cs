namespace UniCore.Application.Modules.Notifications
{
    // Created = false: nothing was saved (the same event was already delivered, or the user turned both channels off).
    public sealed record NotificationPublishResult(bool Created, bool InApp, bool EmailQueued);


}
