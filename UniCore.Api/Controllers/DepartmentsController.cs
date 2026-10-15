using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.University;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.University;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/departments")]
    [Authorize]
    public sealed class DepartmentsController : ControllerBase
    {
        // Permissions and scope are checked inside the handler.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] DepartmentManagementHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null, [FromQuery] Guid? facultyId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, facultyId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{departmentId:guid}")]
        public async Task<IActionResult> Get(Guid departmentId, [FromServices] DepartmentManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(departmentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request, [FromServices] DepartmentManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateDepartmentCommand(request.FacultyId ?? Guid.Empty, request.Code, request.Name, request.Description, request.Email, request.Phone);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/departments/{summary.Id}", summary));
        }

        [HttpPut("{departmentId:guid}")]
        public async Task<IActionResult> Update(Guid departmentId, [FromBody] UpdateDepartmentRequest request, [FromServices] DepartmentManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdateDepartmentCommand(request.Name, request.Description, request.Email, request.Phone);

            return (await handler.UpdateAsync(departmentId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{departmentId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid departmentId, [FromServices] DepartmentManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(departmentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{departmentId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid departmentId, [FromServices] DepartmentManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(departmentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
