using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Me;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/me")]
    [Authorize]
    public sealed class MeController : ControllerBase
    {
        // The caller's own identity and access, loaded from the database (not from the token).
        [HttpGet]
        public IActionResult Get([FromServices] ICurrentUser user) => Ok(new MeResponse(user.UserId,user.PersonId, [.. user.Roles.Order(StringComparer.Ordinal)], [.. user.Permissions.Order(StringComparer.Ordinal)], user.Scopes));

        // Diagnostic: "could I do this?" for the signed-in user only.
        // Example: /api/v1/me/access-check?permission=Grades.Publish&scopeType=Faculty&scopeId=<guid>
        [HttpGet("access-check")]
        public async Task<IActionResult> AccessCheck([FromQuery] string? permission,[FromQuery] ScopeType? scopeType,[FromQuery] Guid? scopeId,[FromServices] IPermissionAuthorizer authorizer,CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(permission))
            {
                return Result.Fail(Error.Validation("PERMISSION_REQUIRED", "A permission code is required.")).ToActionResult(HttpContext);
            }

            var resource = scopeType is null ? null : new ResourceContext(scopeType, scopeId);

            var result = await authorizer.AuthorizeAsync(permission.Trim(), resource, cancellationToken);

            return result.ToActionResult(HttpContext);
        }
    }
}
