using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Graduation;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Graduation;
using UniCore.Domain.Modules.Graduation;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/graduation")]
    [Authorize]
    public sealed class GraduationController : ControllerBase
    {
        // Permissions are checked inside the handlers: Graduation.Read (a student reads their own; heads and deans their department's
        // students; the registrar all), Graduation.Apply (the student), Graduation.Decide (registrar, whole-university scope).

        [HttpGet("students/{studentId:guid}/eligibility")]
        public async Task<IActionResult> Eligibility(Guid studentId, [FromServices] GraduationApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.GetEligibilityAsync(studentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("students/{studentId:guid}/applications")]
        public async Task<IActionResult> Apply(Guid studentId, [FromServices] GraduationApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.ApplyAsync(studentId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("students/{studentId:guid}/applications")]
        public async Task<IActionResult> ListOfStudent(Guid studentId, [FromServices] GraduationApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.ListOfStudentAsync(studentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("applications")]
        public async Task<IActionResult> List([FromServices] GraduationApplicationHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] GraduationApplicationStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("applications/{applicationId:guid}")]
        public async Task<IActionResult> Get(Guid applicationId, [FromServices] GraduationApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(applicationId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("applications/{applicationId:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid applicationId, [FromServices] GraduationApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.CancelAsync(applicationId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("applications/{applicationId:guid}/approve")]
        public async Task<IActionResult> Approve(Guid applicationId, [FromBody] ApproveGraduationRequest? request, [FromServices] GraduationDecisionHandler handler, CancellationToken cancellationToken)
            => (await handler.ApproveAsync(applicationId, request?.Note, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("applications/{applicationId:guid}/reject")]
        public async Task<IActionResult> Reject(Guid applicationId, [FromBody] RejectGraduationRequest request, [FromServices] GraduationDecisionHandler handler, CancellationToken cancellationToken)
            => (await handler.RejectAsync(applicationId, request.Reason, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
