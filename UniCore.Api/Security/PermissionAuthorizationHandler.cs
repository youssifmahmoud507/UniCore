using Microsoft.AspNetCore.Authorization;
using UniCore.Application.Modules.Identity;

namespace UniCore.Api.Security
{
    public sealed class PermissionAuthorizationHandler(IPermissionAuthorizer authorizer) : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var result = await _authorizer.AuthorizeAsync(requirement.Permission);

            if (result.IsSuccess)
            {
                context.Succeed(requirement);
            }

            // Not succeeding leads to 401/403, written as ProblemDetails by the JWT bearer events.
        }
    }

}
