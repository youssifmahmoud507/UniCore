using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Domain.Modules.Idempotency
{
    public enum IdempotencyRecordStatus
    {
        // The first request with this key is still running. A second one must wait (409), not run again.
        InProgress = 0,

        // The first request finished with a success response, which is stored and replayed.
        Completed = 1
    }



}
