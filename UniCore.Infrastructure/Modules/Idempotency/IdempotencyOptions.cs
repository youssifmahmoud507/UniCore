using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Infrastructure.Modules.Idempotency
{
    // appsettings: "Idempotency": { "ExpiryHours": 24, "InProgressTimeoutSeconds": 120 } (both optional)
    public sealed class IdempotencyOptions
    {
        public const string SectionName = "Idempotency";

        // How long a key is remembered (platform-services.md section 6: 24 hours). The cleanup job removes old rows.
        public int ExpiryHours { get; set; } = 24;

        // A request still "running" after this long crashed, and its key may be used again.
        public int InProgressTimeoutSeconds { get; set; } = 120;

        public static bool IsValid(IdempotencyOptions options)
            => options.ExpiryHours is >= 1 and <= 168 && options.InProgressTimeoutSeconds is >= 10 and <= 3600;
    }
}
