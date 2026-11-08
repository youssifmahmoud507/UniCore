using Microsoft.AspNetCore.Mvc;
using UniCore.Application.Modules.Idempotency;

namespace UniCore.Api.Filters
{
    // Sends a stored response again, byte for byte, marked as a replay.
    public sealed class StoredResponseResult(StoredResponse stored) : IActionResult
    {
        private readonly StoredResponse _stored = stored;

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;

            response.StatusCode = _stored.StatusCode;

            if (_stored.ContentType is not null)
            {
                response.ContentType = _stored.ContentType;
            }

            if (_stored.Location is not null)
            {
                response.Headers.Location = _stored.Location;
            }

            response.Headers[IdempotencyFilter.ReplayedHeaderName] = "true";
            response.ContentLength = _stored.Body.Length;

            await response.Body.WriteAsync(_stored.Body, context.HttpContext.RequestAborted);
        }
    }

}
