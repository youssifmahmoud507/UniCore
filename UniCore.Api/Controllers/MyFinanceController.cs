using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Api.Controllers
{
    // The logged-in student's own money. The student is found from the user, so no id is needed.
    [ApiController]
    [Route("api/v1/me")]
    [Authorize]
    public sealed class MyFinanceController : ControllerBase
    {
        [HttpGet("finance")]
        public async Task<IActionResult> Finance([FromServices] MyFinanceHandler handler, CancellationToken cancellationToken)
            => (await handler.GetFinanceAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("invoices")]
        public async Task<IActionResult> Invoices([FromServices] MyFinanceHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] InvoiceStatus? status = null)
            => (await handler.ListInvoicesAsync(new PaginationQuery(page, pageSize), status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("invoices/{id:guid}")]
        public async Task<IActionResult> Invoice(Guid id, [FromServices] MyFinanceHandler handler, CancellationToken cancellationToken)
            => (await handler.GetInvoiceAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("payments")]
        public async Task<IActionResult> Payments([FromServices] MyFinanceHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize)
            => (await handler.ListPaymentsAsync(new PaginationQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);
    }
}
