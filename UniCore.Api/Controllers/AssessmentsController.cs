using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Assessments;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Assessments;
using UniCore.Domain.Modules.Assessments;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/assessments")]
    [Authorize]
    public sealed class AssessmentsController : ControllerBase
    {
        // Permissions are checked inside the handler: Assessments.Read to read, Assessments.Manage (on the section) to change.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] AssessmentHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] Guid? sectionId = null, [FromQuery] AssessmentStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), sectionId, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{assessmentId:guid}")]
        public async Task<IActionResult> Get(Guid assessmentId, [FromServices] AssessmentHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(assessmentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAssessmentRequest request, [FromServices] AssessmentHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.CreateAsync(new CreateAssessmentCommand(request.CourseSectionId ?? Guid.Empty), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/assessments/{summary.Id}", summary));
        }

        [HttpPost("{assessmentId:guid}/components")]
        public async Task<IActionResult> AddComponent(Guid assessmentId, [FromBody] ComponentRequest request, [FromServices] AssessmentHandler handler, CancellationToken cancellationToken)
            => (await handler.AddComponentAsync(assessmentId, ToCommand(request), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{assessmentId:guid}/components/{componentId:guid}")]
        public async Task<IActionResult> UpdateComponent(Guid assessmentId, Guid componentId, [FromBody] ComponentRequest request, [FromServices] AssessmentHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateComponentAsync(assessmentId, componentId, ToCommand(request), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpDelete("{assessmentId:guid}/components/{componentId:guid}")]
        public async Task<IActionResult> RemoveComponent(Guid assessmentId, Guid componentId, [FromServices] AssessmentHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveComponentAsync(assessmentId, componentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{assessmentId:guid}/publish")]
        public async Task<IActionResult> Publish(Guid assessmentId, [FromServices] AssessmentHandler handler, CancellationToken cancellationToken)
            => (await handler.PublishAsync(assessmentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{assessmentId:guid}/close")]
        public async Task<IActionResult> Close(Guid assessmentId, [FromServices] AssessmentHandler handler, CancellationToken cancellationToken)
            => (await handler.CloseAsync(assessmentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{assessmentId:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid assessmentId, [FromServices] AssessmentHandler handler, CancellationToken cancellationToken)
            => (await handler.CancelAsync(assessmentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        // A missing type, weight or maximum score is sent on as an invalid value (0 / undefined) so the domain reports ASSESSMENT_COMPONENT_INVALID.
        private static ComponentCommand ToCommand(ComponentRequest request)
            => new(request.Name, request.Type ?? (ComponentType)(-1), request.Weight ?? 0m, request.MaxScore ?? 0m, request.DueDate);
    }
}
