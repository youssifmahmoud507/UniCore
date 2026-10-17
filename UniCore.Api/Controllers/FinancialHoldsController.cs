using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Finance;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/financial-holds")]
    [Authorize]
    public sealed class FinancialHoldsController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Place([FromBody] PlaceHoldRequest request, [FromServices] FinancialHoldHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.PlaceAsync(new PlaceHoldCommand(request.StudentId, request.Areas, request.Note), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/financial-holds/{created.Id}", created));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] FinancialHoldHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] Guid? studentId = null, [FromQuery] FinancialHoldStatus? status = null, [FromQuery] FinancialHoldReason? reason = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), studentId, status, reason, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] FinancialHoldHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/release")]
        public async Task<IActionResult> Release(Guid id, [FromBody] ReleaseHoldRequest request, [FromServices] FinancialHoldHandler handler, CancellationToken cancellationToken)
            => (await handler.ReleaseAsync(id, request.Note, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("evaluate")]
        public async Task<IActionResult> Evaluate([FromServices] FinancialHoldHandler handler, CancellationToken cancellationToken, [FromBody] EvaluateHoldsRequest? request = null)
            => (await handler.EvaluateAsync(request?.StudentId, cancellationToken)).ToActionResult(HttpContext);
    }

}
