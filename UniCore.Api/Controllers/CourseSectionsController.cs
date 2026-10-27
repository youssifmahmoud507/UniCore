using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Offerings;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Offerings;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/course-sections")]
    [Authorize]
    public sealed class CourseSectionsController : ControllerBase
    {
        // Permissions and scope are checked inside the handlers:
        // Courses.Read to read, Courses.Manage (in the offered course's department) to change.

        [HttpGet("{sectionId:guid}")]
        public async Task<IActionResult> Get(Guid sectionId, [FromServices] CourseSectionHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(sectionId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{sectionId:guid}")]
        public async Task<IActionResult> Update(Guid sectionId, [FromBody] UpdateSectionRequest request, [FromServices] CourseSectionHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(sectionId, new UpdateSectionCommand(request.Capacity), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{sectionId:guid}/close")]
        public async Task<IActionResult> Close(Guid sectionId, [FromServices] CourseSectionHandler handler, CancellationToken cancellationToken)
            => (await handler.CloseAsync(sectionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{sectionId:guid}/reopen")]
        public async Task<IActionResult> Reopen(Guid sectionId, [FromServices] CourseSectionHandler handler, CancellationToken cancellationToken)
            => (await handler.ReopenAsync(sectionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{sectionId:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid sectionId, [FromServices] CourseSectionHandler handler, CancellationToken cancellationToken)
            => (await handler.CancelAsync(sectionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        // ---------- instructors of a section ----------

        [HttpGet("{sectionId:guid}/assignments")]
        public async Task<IActionResult> ListAssignments(Guid sectionId, [FromServices] InstructorAssignmentHandler handler, CancellationToken cancellationToken, [FromQuery] bool activeOnly = false)
            => (await handler.ListAsync(sectionId, activeOnly, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{sectionId:guid}/assignments")]
        public async Task<IActionResult> Assign(Guid sectionId, [FromBody] AssignInstructorRequest request, [FromServices] InstructorAssignmentHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.AssignAsync(sectionId, new AssignInstructorCommand(request.InstructorId ?? Guid.Empty, request.Role), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/course-sections/{sectionId}/assignments", summary));
        }

        [HttpPost("{sectionId:guid}/assignments/{assignmentId:guid}/end")]
        public async Task<IActionResult> EndAssignment(Guid sectionId, Guid assignmentId, [FromServices] InstructorAssignmentHandler handler, CancellationToken cancellationToken)
            => (await handler.EndAsync(sectionId, assignmentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
