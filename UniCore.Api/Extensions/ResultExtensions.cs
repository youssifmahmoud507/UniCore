using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using UniCore.Domain.Common.Results;

namespace UniCore.Api.Extensions
{
    public static class ResultExtensions
    {
        public static IActionResult ToActionResult(this Result result, HttpContext http, Func<IActionResult>? onSuccess = null)
        {
            if (result.IsFailure)
            {
                return ToProblem(result.Errors, http);
            }

            return onSuccess is null ? new NoContentResult() : onSuccess();
        }

        public static IActionResult ToActionResult<T>(this Result<T> result, HttpContext http, Func<T, IActionResult>? onSuccess = null)
        {
            if (!result.TryGetValue(out var value))
            {
                return ToProblem(result.Errors, http);
            }

            return onSuccess is null ? new OkObjectResult(value) : onSuccess(value);
        }

        private static ObjectResult ToProblem(IReadOnlyList<Error> errors, HttpContext http)
        {
            var primary = errors.Count > 0 ? errors[0] : Error.Failure();
            var status = StatusCodeFor(primary.ErrorType);

            var problem = new ProblemDetails
            {
                Status = status,
                Title = TitleFor(status),
                Detail = primary.Description,
                Type = $"urn:unicore:error:{primary.Code}",
                Instance = http.Request.Path
            };

            problem.Extensions["errorCode"] = primary.Code;
            problem.Extensions["traceId"] = http.TraceIdentifier;
            if (primary.ErrorType == ErrorType.Validation && errors.Count > 0)
            {
                problem.Extensions["errors"] = errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            }

            return new ObjectResult(problem)
            {
                StatusCode = status,
                ContentTypes = { "application/problem+json" }
            };
        }

        private static int StatusCodeFor(ErrorType type) => type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Concurrency => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.InvalidCredentials => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            ErrorType.External => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        private static string TitleFor(int status) => status switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
            StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
            _ => "Internal Server Error"
        };
    }
}
