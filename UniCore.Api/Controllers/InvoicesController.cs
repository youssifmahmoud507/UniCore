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
    [Route("api/v1/invoices")]
    [Authorize]
    public sealed class InvoicesController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Issue([FromBody] IssueInvoiceRequest request, [FromServices] InvoiceHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.IssueAsync(new IssueInvoiceCommand(request.StudentId, request.AcademicSemesterId, request.DueDate), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, issued => Created($"/api/v1/invoices/{issued.Id}", issued));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] InvoiceHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] Guid? studentId = null, [FromQuery] InvoiceStatus? status = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), studentId, status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] InvoiceHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);
    }
}
