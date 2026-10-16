using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.People;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.People;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/teaching-assistants")]
    [Authorize]
    public sealed class TeachingAssistantsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] TeachingAssistantHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? sort = null, [FromQuery] bool? isActive = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), sort, isActive, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{assistantId:guid}")]
        public async Task<IActionResult> Get(Guid assistantId, [FromServices] TeachingAssistantHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(assistantId, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTeachingAssistantRequest request, [FromServices] TeachingAssistantHandler handler, CancellationToken cancellationToken)
        {
            var result = await handler.CreateAsync(new CreateTeachingAssistantCommand(request.EmployeeId, request.StudentId), this.GetRequestInfo(), cancellationToken);
            return result.ToActionResult(HttpContext, summary => Created($"/api/v1/teaching-assistants/{summary.Id}", summary));
        }

        [HttpPost("{assistantId:guid}/activate")]
        public async Task<IActionResult> Activate(Guid assistantId, [FromServices] TeachingAssistantHandler handler, CancellationToken cancellationToken)
            => (await handler.ActivateAsync(assistantId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{assistantId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid assistantId, [FromServices] TeachingAssistantHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(assistantId, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
