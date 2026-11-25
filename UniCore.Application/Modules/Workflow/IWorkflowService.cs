using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Workflow
{
    // The contract the request modules (Developer 1) call. Every method checks who the caller is.
    // The engine only moves the state; side effects happen when the request modules react to the workflow events.
    public interface IWorkflowService
    {
        // The caller becomes the requester. Fails with WORKFLOW_ALREADY_ACTIVE if the request already has a live workflow.
        Task<Result<WorkflowInstanceDto>> StartAsync(StartWorkflowCommand command, RequestInfo? request = null, CancellationToken cancellationToken = default);

        // For a request module that has ALREADY authorized the caller with its own permission (for example GradeChanges.Request in the
        // department of the grade). Same as StartAsync without the Requests.Submit check, which only students hold.
        Task<Result<WorkflowInstanceDto>> StartAuthorizedAsync(StartWorkflowCommand command, RequestInfo? request = null, CancellationToken cancellationToken = default);

        Task<Result<WorkflowInstanceDto>> ApproveAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default);

        Task<Result<WorkflowInstanceDto>> RejectAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default);

        Task<Result<WorkflowInstanceDto>> ReturnAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default);

        Task<Result<WorkflowInstanceDto>> ResubmitAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default);

        Task<Result<WorkflowInstanceDto>> CancelAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default);

        Task<Result<WorkflowInstanceDto>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);
    }



}
