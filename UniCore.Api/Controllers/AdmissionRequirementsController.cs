using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Admissions;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Admissions;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/admission-requirements")]
    [Authorize]
    public sealed class AdmissionRequirementsController : ControllerBase
    {
        // Permissions and scope are checked inside the handler.

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] Guid programId, [FromServices] AdmissionRequirementHandler handler, CancellationToken cancellationToken, [FromQuery] bool activeOnly = false)
            => (await handler.ListAsync(programId, activeOnly, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRequirementRequest request, [FromServices] AdmissionRequirementHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateRequirementCommand(request.AcademicProgramId ?? Guid.Empty, request.Code, request.Name, request.Description, request.IsMandatory ?? true);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/admission-requirements?programId={summary.AcademicProgramId}", summary));
        }

        [HttpPut("{requirementId:guid}")]
        public async Task<IActionResult> Update(Guid requirementId, [FromBody] UpdateRequirementRequest request, [FromServices] AdmissionRequirementHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(requirementId, new UpdateRequirementCommand(request.Name, request.Description, request.IsMandatory ?? true), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{requirementId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid requirementId, [FromServices] AdmissionRequirementHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(requirementId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{requirementId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid requirementId, [FromServices] AdmissionRequirementHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(requirementId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
