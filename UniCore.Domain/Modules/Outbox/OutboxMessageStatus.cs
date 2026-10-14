using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Domain.Modules.Outbox
{
    public enum OutboxMessageStatus
    {
        // Waiting for its turn (new, or failed and waiting for the next attempt).
        Pending = 0,

        // Claimed by a processor. If the processor dies, the claim expires (LockedUntil).
        Processing = 1,

        Processed = 2,

        // Poison message: failed for good. Never retried by itself.
        Failed = 3
    }



}
