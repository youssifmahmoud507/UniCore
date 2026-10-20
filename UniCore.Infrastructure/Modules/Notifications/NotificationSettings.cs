using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Notifications;

namespace UniCore.Infrastructure.Modules.Notifications
{
    // The application name in the emails is the one the auth emails already use.
    public sealed class NotificationSettings(IPasswordResetSettings settings) : INotificationSettings
    {
        public string AppName => settings.AppName;
    }

}
