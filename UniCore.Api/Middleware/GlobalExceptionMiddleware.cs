namespace UniCore.Api.Middleware
{
    public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger = logger;

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request was cancelled by the client.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception. CorrelationId {CorrelationId}", context.TraceIdentifier);

                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.Clear();

                var problem = Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An unexpected error occurred.",
                    type: "urn:unicore:error:INTERNAL_SERVER_ERROR",
                    instance: context.Request.Path,
                    extensions: new Dictionary<string, object?>
                    {
                        ["errorCode"] = "INTERNAL_SERVER_ERROR",
                        ["traceId"] = context.TraceIdentifier
                    });

                await problem.ExecuteAsync(context);
            }
        }
    }
}
