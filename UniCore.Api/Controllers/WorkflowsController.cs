using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UniCore.Api.Contracts.Workflow;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Api.Controllers
{
    [ApiController]
    [Route("api/v1/workflows")]
    [Authorize]
    public sealed class WorkflowsController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Start([FromBody] StartWorkflowRequest request, [FromServices] IWorkflowService service, CancellationToken cancellationToken)
        {
            var result = await service.StartAsync(new StartWorkflowCommand(request.DefinitionCode, request.ReferenceType, request.ReferenceId, request.SubjectStudentId, request.ScopeType, request.ScopeId), this.GetRequestInfo(), cancellationToken);

            return result.ToActionResult(HttpContext, started => Created($"/api/v1/workflows/{started.Id}", started));
        }

        // Staff with a Global scope see every instance.
        [HttpGet]
        public async Task<IActionResult> List([FromServices] WorkflowQueryHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] WorkflowInstanceStatus? status = null, [FromQuery] string? referenceType = null, [FromQuery] Guid? referenceId = null, [FromQuery] Guid? subjectStudentId = null)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), status, referenceType, referenceId, subjectStudentId, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("mine")]
        public async Task<IActionResult> Mine([FromServices] WorkflowQueryHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] WorkflowInstanceStatus? status = null)
            => (await handler.MineAsync(new PaginationQuery(page, pageSize), status, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("inbox")]
        public async Task<IActionResult> Inbox([FromServices] WorkflowQueryHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize)
            => (await handler.InboxAsync(new PaginationQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, [FromServices] IWorkflowService service, CancellationToken cancellationToken)
            => (await service.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> Approve(Guid id, [FromServices] IWorkflowService service, CancellationToken cancellationToken, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkflowCommentRequest? request = null)
            => (await service.ApproveAsync(id, request?.Comment, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> Reject(Guid id, [FromServices] IWorkflowService service, CancellationToken cancellationToken, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkflowCommentRequest? request = null)
            => (await service.RejectAsync(id, request?.Comment, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/return")]
        public async Task<IActionResult> Return(Guid id, [FromServices] IWorkflowService service, CancellationToken cancellationToken, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkflowCommentRequest? request = null)
            => (await service.ReturnAsync(id, request?.Comment, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/resubmit")]
        public async Task<IActionResult> Resubmit(Guid id, [FromServices] IWorkflowService service, CancellationToken cancellationToken, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkflowCommentRequest? request = null)
            => (await service.ResubmitAsync(id, request?.Comment, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, [FromServices] IWorkflowService service, CancellationToken cancellationToken, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkflowCommentRequest? request = null)
            => (await service.CancelAsync(id, request?.Comment, this.GetRequestInfo(), cancellationToken)).ToActionResult(HttpContext);
    }


}
