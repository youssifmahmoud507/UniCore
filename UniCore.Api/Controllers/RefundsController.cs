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
    [Route("api/v1/refunds")]
    [Authorize]
    public sealed class RefundsController : ControllerBase
    {
        [HttpPost]
        public new async Task<IActionResult> Request([FromBody] RequestRefundRequest request, [FromServices] RefundHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.RequestAsync(new RequestRefundCommand(request.PaymentId, request.Amount, request.Reason), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/refunds/{created.Id}", created));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] RefundHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] RefundStatus? status = null, [FromQuery] Guid? studentId = null, [FromQuery] Guid? invoiceId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), status, studentId, invoiceId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] RefundHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> Approve(Guid id, [FromServices] RefundHandler handler, CancellationToken cancellationToken)
            => (await handler.ApproveAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRefundRequest request, [FromServices] RefundHandler handler, CancellationToken cancellationToken)
            => (await handler.RejectAsync(id, request.Reason, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/process")]
        public async Task<IActionResult> Process(Guid id, [FromServices] RefundHandler handler, CancellationToken cancellationToken)
            => (await handler.ProcessAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }

}
