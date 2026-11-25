using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.StudentServices;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.StudentServices;
using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/student-services/transcript-requests")]
    [Authorize]
    public sealed class TranscriptRequestsController : ControllerBase
    {
        // Permissions are checked inside the handler: Requests.Submit (the student), Requests.Read (a student reads their own;
        // heads and deans their department's students; the registrar all). A request the caller may not see is "not found".
        // Approving, rejecting and returning go through the workflow engine (/api/v1/workflows), not through this controller.

        [HttpPost("students/{studentId:guid}")]
        public async Task<IActionResult> Submit(Guid studentId, [FromBody] SubmitTranscriptRequest request, [FromServices] TranscriptRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.SubmitAsync(studentId, request.Purpose, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("students/{studentId:guid}")]
        public async Task<IActionResult> ListOfStudent(Guid studentId, [FromServices] TranscriptRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.ListOfStudentAsync(studentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet]
        public async Task<IActionResult> List([FromServices] TranscriptRequestHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] ServiceRequestStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{requestId:guid}")]
        public async Task<IActionResult> Get(Guid requestId, [FromServices] TranscriptRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(requestId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{requestId:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid requestId, [FromServices] TranscriptRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.CancelAsync(requestId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{requestId:guid}/resubmit")]
        public async Task<IActionResult> Resubmit(Guid requestId, [FromServices] TranscriptRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.ResubmitAsync(requestId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
