using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Registrations;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Modules.Registrations;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/registrations")]
    [Authorize]
    public sealed class RegistrationsController : ControllerBase
    {
        // Permissions and scope are checked inside the handler: Registration.Register / Registration.Manage to change,
        // Registration.Read to read; students only reach their own registrations.

        // Registrations of one student (studentId is required).
        [HttpGet]
        public async Task<IActionResult> List([FromServices] RegistrationHandler handler, CancellationToken cancellationToken, [FromQuery] Guid studentId, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? sort = null, [FromQuery] Guid? semesterId = null, [FromQuery] RegistrationStatus? status = null)
            => (await handler.ListAsync(studentId, semesterId, status, sort, new PaginationQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("me")]
        public async Task<IActionResult> GetMine([FromQuery] Guid semesterId, [FromServices] RegistrationHandler handler, CancellationToken cancellationToken)
            => (await handler.GetMineAsync(semesterId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] RegistrationHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        // Takes one seat. The Idempotency-Key header is required: repeating the call with the same key and body
        // returns the first answer (header Idempotent-Replayed: true) and never takes a second seat.
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromServices] RegistrationHandler handler, CancellationToken cancellationToken)
        {
            var command = new RegisterCommand(request.StudentId ?? Guid.Empty, request.AcademicSemesterId ?? Guid.Empty, request.CourseSectionId ?? Guid.Empty, request.OverrideReason);

            var result = await handler.RegisterAsync(command, idempotencyKey, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, outcome =>
            {
                if (outcome.IsReplay)
                {
                    Response.Headers["Idempotent-Replayed"] = "true";
                    return Ok(outcome);
                }

                return Created($"/api/v1/registrations/{outcome.Registration.Id}", outcome);
            });
        }

        [HttpPost("{id:guid}/submit")]
        public async Task<IActionResult> Submit(Guid id, [FromServices] RegistrationHandler handler, CancellationToken cancellationToken)
            => (await handler.SubmitAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, [FromServices] RegistrationHandler handler, CancellationToken cancellationToken)
            => (await handler.CancelAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id, [FromServices] RegistrationHandler handler, CancellationToken cancellationToken)
            => (await handler.CompleteAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/drop")]
        public async Task<IActionResult> Drop(Guid id, [FromBody] DropRequest request, [FromServices] RegistrationHandler handler, CancellationToken cancellationToken)
            => (await handler.DropAsync(id, new DropCommand(request.EnrollmentIds ?? []), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/withdraw")]
        public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawRequest request, [FromServices] RegistrationHandler handler, CancellationToken cancellationToken)
            => (await handler.WithdrawAsync(id, request.EnrollmentId ?? Guid.Empty, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
