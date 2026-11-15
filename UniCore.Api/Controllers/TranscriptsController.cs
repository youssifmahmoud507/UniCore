using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Transcripts;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/transcripts")]
    [Authorize]
    public sealed class TranscriptsController : ControllerBase
    {
        // Permissions are checked inside the handlers: Transcripts.Read (a student reads their own; heads and deans their department's students;
        // the registrar all) and Transcripts.Issue (registrar) for an official transcript.

        [HttpGet("students/{studentId:guid}/gpa")]
        public async Task<IActionResult> Gpa(Guid studentId, [FromServices] TranscriptQueryHandler handler, CancellationToken cancellationToken)
            => (await handler.GetGpaAsync(studentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("students/{studentId:guid}/semesters/{semesterId:guid}/gpa")]
        public async Task<IActionResult> SemesterGpa(Guid studentId, Guid semesterId, [FromServices] TranscriptQueryHandler handler, CancellationToken cancellationToken)
            => (await handler.GetSemesterGpaAsync(studentId, semesterId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("students/{studentId:guid}/history")]
        public async Task<IActionResult> History(Guid studentId, [FromServices] TranscriptQueryHandler handler, CancellationToken cancellationToken)
            => (await handler.GetHistoryAsync(studentId, cancellationToken)).ToActionResult(HttpContext);

        // The unofficial transcript, calculated now.
        [HttpGet("students/{studentId:guid}")]
        public async Task<IActionResult> Transcript(Guid studentId, [FromServices] TranscriptQueryHandler handler, CancellationToken cancellationToken)
            => (await handler.GetTranscriptAsync(studentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("students/{studentId:guid}/snapshots")]
        public async Task<IActionResult> Snapshots(Guid studentId, [FromServices] TranscriptQueryHandler handler, CancellationToken cancellationToken)
            => (await handler.ListSnapshotsAsync(studentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("snapshots/{snapshotId:guid}")]
        public async Task<IActionResult> Snapshot(Guid snapshotId, [FromServices] TranscriptQueryHandler handler, CancellationToken cancellationToken)
            => (await handler.GetSnapshotAsync(snapshotId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("students/{studentId:guid}/snapshots")]
        public async Task<IActionResult> Issue(Guid studentId, [FromServices] TranscriptIssueHandler handler, CancellationToken cancellationToken)
            => (await handler.IssueAsync(studentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
