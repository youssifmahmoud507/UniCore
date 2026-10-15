using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.University;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.University;
using UniCore.Domain.Modules.University;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/academic-programs")]
    [Authorize]
    public sealed class AcademicProgramsController : ControllerBase
    {
        // Permissions and scope are checked inside the handler.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] ProgramManagementHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null, [FromQuery] Guid? departmentId = null, [FromQuery] DegreeType? degreeType = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, departmentId, degreeType, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{programId:guid}")]
        public async Task<IActionResult> Get(Guid programId, [FromServices] ProgramManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(programId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProgramRequest request, [FromServices] ProgramManagementHandler handler, CancellationToken cancellationToken)
        {
            // A missing number becomes 0 here, which the domain rejects with INVALID_DURATION / INVALID_REQUIRED_CREDITS.
            var command = new CreateProgramCommand(request.DepartmentId ?? Guid.Empty, request.Code, request.Name, request.DegreeType, request.DurationYears ?? 0, request.TotalRequiredCredits ?? 0);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/academic-programs/{summary.Id}", summary));
        }

        [HttpPut("{programId:guid}")]
        public async Task<IActionResult> Update(Guid programId, [FromBody] UpdateProgramRequest request, [FromServices] ProgramManagementHandler handler, CancellationToken cancellationToken)
        {
            var command = new UpdateProgramCommand(request.Name, request.DegreeType, request.DurationYears ?? 0, request.TotalRequiredCredits ?? 0);

            return (await handler.UpdateAsync(programId, command, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{programId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid programId, [FromServices] ProgramManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(programId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{programId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid programId, [FromServices] ProgramManagementHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(programId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
