using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Finance;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Finance;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/fee-definitions")]
    [Authorize]
    public sealed class FeeDefinitionsController : ControllerBase
    {
        // Permissions are checked inside the handler.

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFeeDefinitionRequest request, [FromServices] FeeCatalogHandler handler, CancellationToken cancellationToken)
        {
            var command = new CreateFeeDefinitionCommand(request.UniversityId ?? Guid.Empty, request.Code, request.Name, request.Description, request.DefaultAmount);

            var result = await handler.CreateAsync(command, this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/fee-definitions/{created.Id}", created));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] FeeCatalogHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] bool? isActive = null, [FromQuery] string? search = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), isActive, search, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] FeeCatalogHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFeeDefinitionRequest request, [FromServices] FeeCatalogHandler handler, CancellationToken cancellationToken)
            => (await handler.UpdateAsync(id, new UpdateFeeDefinitionCommand(request.Name, request.Description, request.DefaultAmount), this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/activate")]
        public async Task<IActionResult> Activate(Guid id, [FromServices] FeeCatalogHandler handler, CancellationToken cancellationToken)
            => (await handler.SetActiveAsync(id, true, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id, [FromServices] FeeCatalogHandler handler, CancellationToken cancellationToken)
            => (await handler.SetActiveAsync(id, false, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }


}
