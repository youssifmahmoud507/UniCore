using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Attendance;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Attendance;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/attendance/policies")]
    [Authorize]
    public sealed class AttendancePoliciesController : ControllerBase
    {
        // Permissions are checked inside the handlers: Attendance.Read to read, Attendance.Manage (in the department) to change.

        [HttpGet]
        public async Task<IActionResult> List([FromServices] AttendancePolicyHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] bool? activeOnly = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), activeOnly, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{policyId:guid}")]
        public async Task<IActionResult> Get(Guid policyId, [FromServices] AttendancePolicyHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(policyId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePolicyRequest request, [FromServices] AttendancePolicyHandler handler, CancellationToken cancellationToken)
        {
            if (request.MinimumPercentage is not { } minimum || request.WarningPercentage is not { } warning || request.LateThresholdMinutes is not { } late)
            {
                return Result<PolicySummary>.Fail(AttendanceErrors.PolicyInvalid).ToActionResult(HttpContext);
            }

            var command = new CreatePolicyCommand(request.AcademicProgramId, request.CourseId, minimum, warning, late, request.IsExamBlocking ?? false);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/attendance/policies/{summary.Id}", summary));
        }

        [HttpPut("{policyId:guid}")]
        public async Task<IActionResult> Update(Guid policyId, [FromBody] UpdatePolicyRequest request, [FromServices] AttendancePolicyHandler handler, CancellationToken cancellationToken)
        {
            if (request.MinimumPercentage is not { } minimum || request.WarningPercentage is not { } warning || request.LateThresholdMinutes is not { } late)
            {
                return Result<PolicySummary>.Fail(AttendanceErrors.PolicyInvalid).ToActionResult(HttpContext);
            }

            return (await handler.UpdateAsync(policyId, new UpdatePolicyCommand(minimum, warning, late, request.IsExamBlocking ?? false), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
        }

        [HttpPost("{policyId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid policyId, [FromServices] AttendancePolicyHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(policyId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
