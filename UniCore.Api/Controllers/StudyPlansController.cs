using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Curriculum;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Curriculum;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/study-plans")]
    [Authorize]
    public sealed class StudyPlansController : ControllerBase
    {
        // Permissions and scope are checked inside the handlers:
        // Courses.Read to read, Curriculum.Manage (in the program's department) to change.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] StudyPlanHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? search = null, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null, [FromQuery] Guid? academicProgramId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), search, sort, isActive, academicProgramId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{planId:guid}")]
        public async Task<IActionResult> Get(Guid planId, [FromServices] StudyPlanHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(planId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateStudyPlanRequest request, [FromServices] StudyPlanHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateStudyPlanCommand(request.AcademicProgramId ?? Guid.Empty, request.Code, request.Name, request.Description);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/study-plans/{summary.Id}", summary));
        }

        [HttpPut("{planId:guid}")]
        public async Task<IActionResult> Update(Guid planId, [FromBody] UpdateStudyPlanRequest request, [FromServices] StudyPlanHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(planId, new UpdateStudyPlanCommand(request.Name, request.Description), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{planId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid planId, [FromServices] StudyPlanHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(planId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{planId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid planId, [FromServices] StudyPlanHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(planId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        // ---------- versions of a plan ----------

        [HttpGet("{planId:guid}/versions")]
        public async Task<IActionResult> ListVersions(Guid planId, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
            => (await handler.ListAsync(planId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{planId:guid}/versions")]
        public async Task<IActionResult> CreateVersion(Guid planId, [FromBody] CreateVersionRequest request, [FromServices] StudyPlanVersionHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.CreateAsync(planId, new CreateVersionCommand(request.SourceVersionId, request.EffectiveFrom), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, detail => Created($"/api/v1/study-plan-versions/{detail.Version.Id}", detail));
        }
    }
}
