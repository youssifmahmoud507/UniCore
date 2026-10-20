using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Admissions;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Admissions;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/admission-applications")]
    [Authorize]
    public sealed class AdmissionApplicationsController : ControllerBase
    {
        // Permissions and scope are checked inside the handler:
        // Admissions.Read (list, get), Admissions.Review (everything up to the decision), Admissions.Decide (decisions).

        [HttpGet]
        public async Task<IActionResult> List([FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? sort = null, [FromQuery] AdmissionApplicationStatus? status = null, [FromQuery] Guid? programId = null, [FromQuery] Guid? academicYearId = null, [FromQuery] Guid? applicantId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), sort, status, programId, academicYearId, applicantId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{applicationId:guid}")]
        public async Task<IActionResult> Get(Guid applicationId, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(applicationId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateApplicationRequest request, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateApplicationCommand(request.ApplicantId ?? Guid.Empty, request.AcademicProgramId ?? Guid.Empty, request.AcademicYearId ?? Guid.Empty);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, detail => Created($"/api/v1/admission-applications/{detail.Application.Id}", detail));
        }

        [HttpPut("{applicationId:guid}/requirements/{requirementId:guid}/document")]
        public async Task<IActionResult> AttachDocument(Guid applicationId, Guid requirementId, [FromBody] AttachDocumentRequest request, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.AttachDocumentAsync(applicationId, requirementId, new AttachDocumentCommand(request.DocumentId ?? Guid.Empty), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{applicationId:guid}/submit")]
        public async Task<IActionResult> Submit(Guid applicationId, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.SubmitAsync(applicationId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{applicationId:guid}/withdraw")]
        public async Task<IActionResult> Withdraw(Guid applicationId, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.WithdrawAsync(applicationId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{applicationId:guid}/start-review")]
        public async Task<IActionResult> StartReview(Guid applicationId, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.StartReviewAsync(applicationId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{applicationId:guid}/requirements/{requirementId:guid}/review")]
        public async Task<IActionResult> ReviewRequirement(Guid applicationId, Guid requirementId, [FromBody] ReviewRequirementRequest request, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.ReviewRequirementAsync(applicationId, requirementId, new ReviewRequirementCommand(request.Approve, request.Note), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{applicationId:guid}/accept")]
        public async Task<IActionResult> Accept(Guid applicationId, [FromBody] DecisionRequest request, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.AcceptAsync(applicationId, new DecisionCommand(request.Reason), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{applicationId:guid}/conditionally-accept")]
        public async Task<IActionResult> ConditionallyAccept(Guid applicationId, [FromBody] DecisionRequest request, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.ConditionallyAcceptAsync(applicationId, new DecisionCommand(request.Reason), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{applicationId:guid}/complete-conditions")]
        public async Task<IActionResult> CompleteConditions(Guid applicationId, [FromBody] DecisionRequest request, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.CompleteConditionsAsync(applicationId, new DecisionCommand(request.Reason), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{applicationId:guid}/reject")]
        public async Task<IActionResult> Reject(Guid applicationId, [FromBody] DecisionRequest request, [FromServices] AdmissionApplicationHandler handler, CancellationToken cancellationToken)
            => (await handler.RejectAsync(applicationId, new DecisionCommand(request.Reason), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
