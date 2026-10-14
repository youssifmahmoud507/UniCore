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
    [Route("api/v1/fee-structures")]
    [Authorize]
    public sealed class FeeStructuresController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFeeStructureRequest request, [FromServices] FeeStructureHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.CreateAsync(new CreateFeeStructureCommand(request.AcademicProgramId, request.AcademicSemesterId, request.Name), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/fee-structures/{created.Id}", created));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] FeeStructureHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] FeeStructureStatus? status = null, [FromQuery] Guid? academicProgramId = null, [FromQuery] Guid? academicSemesterId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), status, academicProgramId, academicSemesterId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] FeeStructureHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/items")]
        public async Task<IActionResult> AddItem(Guid id, [FromBody] AddFeeItemRequest request, [FromServices] FeeStructureHandler handler, CancellationToken cancellationToken)
            => (await handler.AddItemAsync(id, request.FeeDefinitionId, request.Amount, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("{id:guid}/items/{itemId:guid}")]
        public async Task<IActionResult> ChangeItem(Guid id, Guid itemId, [FromBody] ChangeFeeItemRequest request, [FromServices] FeeStructureHandler handler, CancellationToken cancellationToken)
            => (await handler.ChangeItemAmountAsync(id, itemId, request.Amount, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpDelete("{id:guid}/items/{itemId:guid}")]
        public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, [FromServices] FeeStructureHandler handler, CancellationToken cancellationToken)
            => (await handler.RemoveItemAsync(id, itemId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/activate")]
        public async Task<IActionResult> Activate(Guid id, [FromServices] FeeStructureHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/archive")]
        public async Task<IActionResult> Archive(Guid id, [FromServices] FeeStructureHandler handler, CancellationToken cancellationToken)
            => (await handler.ArchiveAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }


}
