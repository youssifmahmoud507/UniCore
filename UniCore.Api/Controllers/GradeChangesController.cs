using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Grading;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Grading;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/grade-changes")]
    [Authorize]
    public sealed class GradeChangesController : ControllerBase
    {
        // A request needs GradeChanges.Request in the department of the grade's course. It starts the GRADE_CHANGE workflow, which the
        // department head and the dean approve through /api/v1/workflows/{id}/approve; the correction is applied when it completes.

        [HttpPost]
        public new async Task<IActionResult> Request([FromBody] RequestGradeChangeRequest request, [FromServices] GradeChangeHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.RequestAsync(new RequestGradeChangeCommand(request.GradeId, request.AssessmentComponentId, request.NewScore, request.Reason), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/grade-changes/{created.Id}", created));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] GradeChangeHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("grades/{gradeId:guid}")]
        public async Task<IActionResult> ListOfGrade(Guid gradeId, [FromServices] GradeChangeHandler handler, CancellationToken cancellationToken)
            => (await handler.ListOfGradeAsync(gradeId, cancellationToken)).ToActionResult(HttpContext);
    }
}
