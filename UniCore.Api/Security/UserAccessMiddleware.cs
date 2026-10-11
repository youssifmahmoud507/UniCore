using UniCore.Application.Modules.Identity;

namespace UniCore.Api.Security
{
    // Runs after authentication: loads the signed-in user's roles, permissions and scopes once per request.
    public sealed class UserAccessMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context, IUserAccessProvider provider, IUserAccessContext accessContext)
        {
            var subject = context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirst(AppClaimTypes.Sub)?.Value
                : null;

            if (Guid.TryParse(subject, out var userId))
            {
                var access = await provider.GetAsync(userId, context.RequestAborted);

                if (!access.TryGetValue(out var loaded))
                {
                    await Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Service Unavailable",
                        detail: "The request could not be authorized right now. Try again later.",
                        type: "urn:unicore:error:EXTERNAL_SERVICE_UNAVAILABLE",
                        instance: context.Request.Path,
                        extensions: new Dictionary<string, object?>
                        {
                            ["errorCode"] = "EXTERNAL_SERVICE_UNAVAILABLE",
                            ["traceId"] = context.TraceIdentifier
                        }).ExecuteAsync(context);

                    return;
                }

                accessContext.Set(loaded);
            }

            await _next(context);
        }
    }

}
