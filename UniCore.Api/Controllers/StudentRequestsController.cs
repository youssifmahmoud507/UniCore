using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.StudentRequests;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.StudentRequests;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1")]
    [Authorize]
    public sealed class StudentRequestsController : ControllerBase
    {
        // A student asks for a withdrawal or a leave of absence on their own record. Each request starts its workflow
        // (WITHDRAWAL_REQUEST or LEAVE_REQUEST), approved through /api/v1/workflows/{id}/approve; the withdrawal or the
        // status change is applied when the workflow completes.

        [HttpPost("withdrawal-requests")]
        public async Task<IActionResult> SubmitWithdrawal([FromBody] SubmitWithdrawalRequest request, [FromServices] StudentRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.SubmitWithdrawalAsync(new SubmitWithdrawalCommand(request.RegistrationId, request.EnrollmentId, request.Reason), this.GetRequestInfo(), cancellationToken))
                .ToActionResult(HttpContext, created => Created($"/api/v1/withdrawal-requests/{created.Id}", created));

        [HttpGet("withdrawal-requests/mine")]
        public async Task<IActionResult> MyWithdrawals([FromServices] StudentRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.MyWithdrawalsAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("withdrawal-requests/{id:guid}")]
        public async Task<IActionResult> GetWithdrawal(Guid id, [FromServices] StudentRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.GetWithdrawalAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("leave-requests")]
        public async Task<IActionResult> SubmitLeave([FromBody] SubmitLeaveRequest request, [FromServices] StudentRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.SubmitLeaveAsync(new SubmitLeaveCommand(request.StudentId, request.StartDate, request.ExpectedReturnDate, request.Reason), this.GetRequestInfo(), cancellationToken))
                .ToActionResult(HttpContext, created => Created($"/api/v1/leave-requests/{created.Id}", created));

        [HttpGet("leave-requests/mine")]
        public async Task<IActionResult> MyLeaves([FromServices] StudentRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.MyLeavesAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("leave-requests/{id:guid}")]
        public async Task<IActionResult> GetLeave(Guid id, [FromServices] StudentRequestHandler handler, CancellationToken cancellationToken)
            => (await handler.GetLeaveAsync(id, cancellationToken)).ToActionResult(HttpContext);
    }
}
