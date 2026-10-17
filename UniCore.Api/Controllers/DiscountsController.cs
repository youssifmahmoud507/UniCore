using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Finance;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Finance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/discounts")]
    [Authorize]
    public sealed class DiscountsController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDiscountRequest request, [FromServices] DiscountHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateDiscountCommand(request.Code, request.Name, request.Description, request.Type, request.Value, request.ValidFrom, request.ValidTo);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/discounts/{created.Id}", created));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] DiscountHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] bool? isActive = null, [FromQuery] string? search = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), isActive, search, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] DiscountHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDiscountRequest request, [FromServices] DiscountHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(id, new UpdateDiscountCommand(request.Name, request.Description, request.Type, request.Value, request.ValidFrom, request.ValidTo), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/activate")]
        public async Task<IActionResult> Activate(Guid id, [FromServices] DiscountHandler handler, CancellationToken cancellationToken)
            => (await handler.SetActiveAsync(id, true, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id, [FromServices] DiscountHandler handler, CancellationToken cancellationToken)
            => (await handler.SetActiveAsync(id, false, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
