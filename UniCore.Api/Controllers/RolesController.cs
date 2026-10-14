using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Roles;
using UniCore.Api.Extensions;
using UniCore.Api.Security;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/roles")]
    [Authorize]
    public sealed class RolesController : ControllerBase
    {
        [HttpGet("~/api/v1/permissions")]
        [HasPermission(Permissions.Roles.Manage)]
        public async Task<IActionResult> ListPermissions([FromServices] RoleManagementHandler handler, CancellationToken cancellationToken)=> (await handler.ListPermissionsAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet]
        [HasPermission(Permissions.Roles.Manage)]
        public async Task<IActionResult> List([FromServices] RoleManagementHandler handler, CancellationToken cancellationToken)=> (await handler.ListRolesAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{roleId:guid}")]
        [HasPermission(Permissions.Roles.Manage)]
        public async Task<IActionResult> Get(Guid roleId, [FromServices] RoleManagementHandler handler, CancellationToken cancellationToken)=> (await handler.GetRoleAsync(roleId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        [HasPermission(Permissions.Roles.Manage)]
        public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, [FromServices] RoleManagementHandler handler,CancellationToken cancellationToken)
        {
            var result = await handler.CreateRoleAsync(request.Name, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, role => Created($"/api/v1/roles/{role.Id}", role));
        }

        [HttpPut("{roleId:guid}/permissions")]
        [HasPermission(Permissions.PermissionAdmin.Manage)]
        public async Task<IActionResult> SetPermissions(Guid roleId, [FromBody] SetRolePermissionsRequest request, [FromServices] RoleManagementHandler handler,CancellationToken cancellationToken)
        {
            var result = await handler.SetRolePermissionsAsync(roleId, request.Permissions, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext);
        }

        [HttpDelete("{roleId:guid}")]
        [HasPermission(Permissions.Roles.Manage)]
        public async Task<IActionResult> Delete(Guid roleId, [FromServices] RoleManagementHandler handler, CancellationToken cancellationToken)=> (await handler.DeleteRoleAsync(roleId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
