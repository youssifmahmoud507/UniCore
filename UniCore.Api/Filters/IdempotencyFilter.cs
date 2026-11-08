using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Idempotency;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Idempotency;

namespace UniCore.Api.Filters
{
    // The work behind [Idempotent]. A resource filter: it runs before model binding and wraps the whole action and its
    // result, so it can see (and keep) the response that was written.
    public sealed class IdempotencyFilter(IIdempotencyService service, ICurrentUser user, ILogger<IdempotencyFilter> logger) : IAsyncResourceFilter
    {
        public const string HeaderName = "Idempotency-Key";
        public const string ReplayedHeaderName = "Idempotency-Replayed";

        // A bigger response is not stored (and the key is freed), because it could not be replayed cheaply.
        public const int MaxStoredBodyBytes = 262_144;

        private readonly IIdempotencyService _service = service;
        private readonly ICurrentUser _user = user;
        private readonly ILogger<IdempotencyFilter> _logger = logger;

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var http = context.HttpContext;

            if (_user.UserId is not { } userId)
            {
                context.Result = Result.Fail(Error.Unauthorized()).ToActionResult(http);
                return;
            }

            var values = http.Request.Headers[HeaderName];

            if (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
            {
                context.Result = Result.Fail(IdempotencyErrors.KeyRequired).ToActionResult(http);
                return;
            }

            if (values.Count > 1)
            {
                context.Result = Result.Fail(IdempotencyErrors.KeyInvalid).ToActionResult(http);
                return;
            }

            var hash = await HashRequestAsync(http.Request, http.RequestAborted);
            var begun = await _service.BeginAsync(values[0], userId, hash, http.RequestAborted);

            if (!begun.TryGetValue(out var begin))
            {
                context.Result = begun.ToActionResult(http);
                return;
            }

            switch (begin.Outcome)
            {
                case IdempotencyOutcome.Replay when begin.Response is not null:
                    context.Result = new StoredResponseResult(begin.Response);
                    return;

                case IdempotencyOutcome.InProgress:
                    context.Result = Result.Fail(IdempotencyErrors.InProgress).ToActionResult(http);
                    return;

                case IdempotencyOutcome.Mismatch:
                    context.Result = Result.Fail(IdempotencyErrors.KeyReused).ToActionResult(http);
                    return;
            }

            await RunAndStoreAsync(http, begin.RecordId, next);
        }

        private async Task RunAndStoreAsync(HttpContext http, Guid recordId, ResourceExecutionDelegate next)
        {
            var original = http.Response.Body;
            await using var buffer = new MemoryStream();
            http.Response.Body = buffer;

            ResourceExecutedContext? executed = null;

            try
            {
                executed = await next();
            }
            finally
            {
                http.Response.Body = original;

                // next() did not come back (it never does after an unhandled failure here): free the key.
                if (executed is null)
                {
                    await FreeAsync(recordId);
                }
            }

            var status = http.Response.StatusCode;
            var body = buffer.ToArray();
            var failed = executed.Exception is not null && !executed.ExceptionHandled;
            var storable = !failed && !executed.Canceled && status is >= 200 and <= 299 && body.Length <= MaxStoredBodyBytes;

            if (storable)
            {
                var location = http.Response.Headers.Location.ToString();
                var stored = new StoredResponse(status, http.Response.ContentType, string.IsNullOrEmpty(location) ? null : location, body);

                // CancellationToken.None: the work is done, a client that went away must not leave the key stuck.
                var completed = await _service.CompleteAsync(recordId, stored, CancellationToken.None);

                if (completed.IsFailure)
                {
                    _logger.LogWarning("The response for an idempotency key could not be stored ({ErrorCode}); the key was freed.", completed.Errors[0].Code);
                    await FreeAsync(recordId);
                }
            }
            else
            {
                await FreeAsync(recordId);
            }

            if (body.Length > 0)
            {
                await original.WriteAsync(body, http.RequestAborted);
            }
        }

        private async Task FreeAsync(Guid recordId)
        {
            var abandoned = await _service.AbandonAsync(recordId, CancellationToken.None);

            if (abandoned.IsFailure)
            {
                _logger.LogWarning("An idempotency key could not be freed ({ErrorCode}); it stays blocked until it goes stale.", abandoned.Errors[0].Code);
            }
        }

        // The same key is only "the same request" when the method, the address and the body are the same.
        private static async Task<string> HashRequestAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            request.EnableBuffering();

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path}{request.QueryString}\n"));

            using var body = new MemoryStream();
            await request.Body.CopyToAsync(body, cancellationToken);
            request.Body.Position = 0;

            hash.AppendData(body.GetBuffer().AsSpan(0, (int)body.Length));

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
    }

}
