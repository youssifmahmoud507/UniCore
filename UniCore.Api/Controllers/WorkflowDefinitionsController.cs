using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Workflow;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/workflow-definitions")]
    [Authorize]
    public sealed class WorkflowDefinitionsController : ControllerBase
    {
        // Publishes a new version of the code (the first one if it is new).
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWorkflowDefinitionRequest request, [FromServices] WorkflowDefinitionHandler handler, CancellationToken cancellationToken)
        {
            var steps = request.Steps?.Select(s => new WorkflowStepInput(s.Name, s.RequiredRole)).ToList();

            var result = await handler.CreateAsync(new CreateWorkflowDefinitionCommand(request.Code, request.Name, steps), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, created => Created($"/api/v1/workflow-definitions/{created.Id}", created));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromServices] WorkflowDefinitionHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? code = null, [FromQuery] bool? isActive = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), code, isActive, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] WorkflowDefinitionHandler handler, CancellationToken cancellationToken)
            => (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id, [FromServices] WorkflowDefinitionHandler handler, CancellationToken cancellationToken)
            => (await handler.DeactivateAsync(id, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }
}
