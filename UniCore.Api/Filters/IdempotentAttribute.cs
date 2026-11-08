using Microsoft.AspNetCore.Mvc;

namespace UniCore.Api.Filters
{
    // Put on an action that must not run twice for one client intention (submit a request, publish a grade, register
    // for a course). conventions.md, "Idempotency":
    //  - the Idempotency-Key header is required (400 IDEMPOTENCY_KEY_REQUIRED when it is missing);
    //  - the same key with the same request replays the first successful response (header Idempotency-Replayed: true);
    //  - the same key with a different request is 409 IDEMPOTENCY_KEY_REUSED;
    //  - the same key while the first request is still running is 409 IDEMPOTENCY_REQUEST_IN_PROGRESS.
    // Only successful (2xx) responses are stored. After a failure the same key can be used again.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class IdempotentAttribute() : TypeFilterAttribute(typeof(IdempotencyFilter));

}
