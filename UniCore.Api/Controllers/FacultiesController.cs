using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.University;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.University;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/faculties")]
    [Authorize]
    public sealed class FacultiesController : ControllerBase
    {
        // Permissions and scope are checked inside the handler.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] FacultyManagementHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null, [FromQuery] Guid? universityId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, universityId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{facultyId:guid}")]
        public async Task<IActionResult> Get(Guid facultyId, [FromServices] FacultyManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(facultyId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFacultyRequest request, [FromServices] FacultyManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateFacultyCommand(request.UniversityId ?? Guid.Empty, request.Code, request.Name, request.Description, request.Email, request.Phone);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/faculties/{summary.Id}", summary));
        }

        [HttpPut("{facultyId:guid}")]
        public async Task<IActionResult> Update(Guid facultyId, [FromBody] UpdateFacultyRequest request, [FromServices] FacultyManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdateFacultyCommand(request.Name, request.Description, request.Email, request.Phone);

            return (await handler.UpdateAsync(facultyId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{facultyId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid facultyId, [FromServices] FacultyManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(facultyId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{facultyId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid facultyId, [FromServices] FacultyManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(facultyId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
