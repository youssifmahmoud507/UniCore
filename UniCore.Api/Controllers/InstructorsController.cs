using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.People;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.People;
using UniCore.Domain.Modules.People;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/instructors")]
    [Authorize]
    public sealed class InstructorsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] InstructorHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null, [FromQuery] AcademicRank? rank = null, [FromQuery] Guid? employeeId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), sort, isActive, rank, employeeId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{instructorId:guid}")]
        public async Task<IActionResult> Get(Guid instructorId, [FromServices] InstructorHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(instructorId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInstructorRequest request, [FromServices] InstructorHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateInstructorCommand(request.EmployeeId ?? Guid.Empty, request.Rank, request.Specialization);
            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);
            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/instructors/{summary.Id}", summary));
        }

        [HttpPut("{instructorId:guid}")]
        public async Task<IActionResult> Update(Guid instructorId, [FromBody] UpdateInstructorRequest request, [FromServices] InstructorHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(instructorId, new UpdateInstructorCommand(request.Rank, request.Specialization), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{instructorId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid instructorId, [FromServices] InstructorHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(instructorId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{instructorId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid instructorId, [FromServices] InstructorHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(instructorId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
