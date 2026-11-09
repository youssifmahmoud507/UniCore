using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Grading;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Grading;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/grades")]
    [Authorize]
    public sealed class GradesController : ControllerBase
    {
        // Permissions are checked inside the handlers: Grades.Enter (assigned instructor) to enter and submit, Grades.Review / Approve /
        // Publish (department of the course) for the later steps, Grades.Read to read (a student sees only their own published grades).

        [HttpGet("{gradeId:guid}")]
        public async Task<IActionResult> Get(Guid gradeId, [FromServices] GradeQueryHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(gradeId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("students/{studentId:guid}")]
        public async Task<IActionResult> ListStudent(Guid studentId, [FromServices] GradeQueryHandler handler, CancellationToken cancellationToken, [FromQuery] Guid? semesterId = null)
            => (await handler.ListStudentAsync(studentId, semesterId, cancellationToken)).ToActionResult(HttpContext);

        // The grade sheet of a section.
        [HttpGet("sections/{sectionId:guid}")]
        public async Task<IActionResult> ListSection(Guid sectionId, [FromServices] GradeQueryHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] GradeStatus? status = null)
            => (await handler.ListSectionAsync(sectionId, new PaginationQuery(page, pageSize), status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("scale")]
        public async Task<IActionResult> Scale([FromServices] GradeQueryHandler handler, CancellationToken cancellationToken)
            => (await handler.GetDefaultScaleAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("sections/{sectionId:guid}/students/{studentId:guid}")]
        public async Task<IActionResult> EnterScores(Guid sectionId, Guid studentId, [FromBody] EnterScoresRequest request, [FromServices] GradeEntryHandler handler, CancellationToken cancellationToken)
        {
            // A missing component id or score is sent on as an empty id / negative score so the handler reports it as invalid.
            var scores = request.Scores?.Select(s => new ScoreInput(s.ComponentId ?? Guid.Empty, s.Score ?? -1m)).ToList();

            return (await handler.EnterScoresAsync(new EnterScoresCommand(sectionId, studentId, scores), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("sections/{sectionId:guid}/submit")]
        public async Task<IActionResult> Submit(Guid sectionId, [FromServices] GradeEntryHandler handler, CancellationToken cancellationToken)
            => (await handler.SubmitSectionAsync(sectionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("sections/{sectionId:guid}/return")]
        public async Task<IActionResult> Return(Guid sectionId, [FromBody] ReturnGradesRequest request, [FromServices] GradeWorkflowHandler handler, CancellationToken cancellationToken)
            => (await handler.ReturnSectionAsync(sectionId, request.Reason, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("sections/{sectionId:guid}/review")]
        public async Task<IActionResult> Review(Guid sectionId, [FromServices] GradeWorkflowHandler handler, CancellationToken cancellationToken)
            => (await handler.ReviewSectionAsync(sectionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("sections/{sectionId:guid}/approve")]
        public async Task<IActionResult> Approve(Guid sectionId, [FromServices] GradeWorkflowHandler handler, CancellationToken cancellationToken)
            => (await handler.ApproveSectionAsync(sectionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("sections/{sectionId:guid}/publish")]
        public async Task<IActionResult> Publish(Guid sectionId, [FromServices] GradeWorkflowHandler handler, CancellationToken cancellationToken)
            => (await handler.PublishSectionAsync(sectionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("sections/{sectionId:guid}/lock")]
        public async Task<IActionResult> Lock(Guid sectionId, [FromServices] GradeWorkflowHandler handler, CancellationToken cancellationToken)
            => (await handler.LockSectionAsync(sectionId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
