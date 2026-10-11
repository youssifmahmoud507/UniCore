using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Users;
using UniCore.Api.Extensions;
using UniCore.Api.Security;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/users")]
    [Authorize]
    public sealed class UsersController : ControllerBase
    {
        [HttpPost]
        [HasPermission(Permissions.Users.Manage)]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request, [FromServices] UserManagementHandler handler,CancellationToken cancellationToken)
        {
            var result = await handler.CreateUserAsync(request.UserName, request.Email, request.PhoneNumber, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, user => Created($"/api/v1/users/{user.Id}", user));
        }

        [HttpGet]
        [HasPermission(Permissions.Users.Read)]
        public async Task<IActionResult> List([FromServices] UserManagementHandler handler,CancellationToken cancellationToken,[FromQuery] int page = 1,[FromQuery] int pageSize = PaginationQuery.DefaultPageSize,[FromQuery] string? search = null,[FromQuery] bool? isActive = null)=> (await handler.ListUsersAsync(new PaginationQuery(page, pageSize), search, isActive, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{userId:guid}")]
        [HasPermission(Permissions.Users.Read)]
        public async Task<IActionResult> Get(Guid userId, [FromServices] UserManagementHandler handler, CancellationToken cancellationToken)=> (await handler.GetUserAsync(userId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{userId:guid}/activate")]
        [HasPermission(Permissions.Users.Manage)]
        public async Task<IActionResult> Activate(Guid userId, [FromServices] UserManagementHandler handler, CancellationToken cancellationToken)=> (await handler.SetActiveAsync(userId, true, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{userId:guid}/deactivate")]
        [HasPermission(Permissions.Users.Manage)]
        public async Task<IActionResult> Deactivate(Guid userId, [FromServices] UserManagementHandler handler, CancellationToken cancellationToken)=> (await handler.SetActiveAsync(userId, false, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{userId:guid}/roles")]
        [HasPermission(Permissions.Roles.Manage)]
        public async Task<IActionResult> AssignRole(Guid userId, [FromBody] AssignRoleRequest request, [FromServices] UserManagementHandler handler,CancellationToken cancellationToken)
        {
            var result = await handler.AssignRoleAsync(userId, request.Role, request.ScopeType, request.ScopeId, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext);
        }

        [HttpDelete("{userId:guid}/roles/{roleName}")]
        [HasPermission(Permissions.Roles.Manage)]
        public async Task<IActionResult> RemoveRole(Guid userId, string roleName, [FromServices] UserManagementHandler handler, CancellationToken cancellationToken)=> (await handler.RemoveRoleAsync(userId, roleName, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpDelete("{userId:guid}/scopes/{scopeAssignmentId:guid}")]
        [HasPermission(Permissions.Roles.Manage)]
        public async Task<IActionResult> RemoveScope(Guid userId, Guid scopeAssignmentId, [FromServices] UserManagementHandler handler,CancellationToken cancellationToken)=> (await handler.RemoveScopeAsync(userId, scopeAssignmentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
