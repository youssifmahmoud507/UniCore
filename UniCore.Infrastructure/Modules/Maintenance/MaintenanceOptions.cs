using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Infrastructure.Modules.Maintenance
{
    // appsettings: "Maintenance": { "ExpiredRequestDays": 60, "OtpRetentionDays": 1, ... } (all optional)
    public sealed class MaintenanceOptions
    {
        public const string SectionName = "Maintenance";

        // Open requests with no activity for this many days are cancelled.
        public int ExpiredRequestDays { get; set; } = 60;

        public int OtpRetentionDays { get; set; } = 1;

        public int RefreshTokenRetentionDays { get; set; } = 30;

        public int ReadNotificationRetentionDays { get; set; } = 90;

        public int UnreadNotificationRetentionDays { get; set; } = 365;

        public int ProcessedOutboxRetentionDays { get; set; } = 14;

        // Messages that failed for good (poison) are kept longer: somebody may need to look at them.
        public int FailedOutboxRetentionDays { get; set; } = 90;

        public static bool IsValid(MaintenanceOptions options) =>
            new[]
            {
                options.ExpiredRequestDays,
                options.OtpRetentionDays,
                options.RefreshTokenRetentionDays,
                options.ReadNotificationRetentionDays,
                options.UnreadNotificationRetentionDays,
                options.ProcessedOutboxRetentionDays,
                options.FailedOutboxRetentionDays
            }.All(days => days is >= 1 and <= 3650);
    }

}
