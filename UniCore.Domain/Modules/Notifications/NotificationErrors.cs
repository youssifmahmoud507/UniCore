using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Notifications
{
    public static class NotificationErrors
    {
        public static readonly Error NotFound = Error.NotFound("NOTIFICATION_NOT_FOUND", "The notification was not found.");
        public static readonly Error DataInvalid = Error.Validation("NOTIFICATION_DATA_INVALID", "The notification data is invalid.");
        public static readonly Error PreferenceInvalid = Error.Validation("NOTIFICATION_PREFERENCE_INVALID", "The notification type is unknown.");
        public static readonly Error PreferenceMandatory = Error.BusinessRule("NOTIFICATION_PREFERENCE_MANDATORY", "This notification type cannot be turned off.");
    }




}
