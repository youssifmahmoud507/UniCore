using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Specifications;
using UniCore.Application.Modules.StudentServices.Specifications;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.StudentServices;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.StudentServices
{
    // What every kind of request does the same way: read, list, cancel, resubmit, and the submit sequence.
    // Order everywhere: permission alone -> load -> scope (a refusal on an existing request looks like "not found") -> state -> change.
    //
    // Submit sequence: the request is saved first (so it has an Id), then the fee is charged (idempotent by the request's Id),
    // then the workflow starts and its instance is attached. If the charge or the workflow fails after the first save, the request is
    // cancelled again and the error is returned, so no request is left Pending without a workflow.
    public abstract class ServiceRequestHandlerBase<T>(
        ServiceRequestAccess access, IAggregateRepository<T> requests, IWorkflowService workflows,
        ICurrentUser user, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit) where T : ServiceRequest
    {
        private readonly ServiceRequestAccess _access = access;
        private readonly IAggregateRepository<T> _requests = requests;
        private readonly IWorkflowService _workflows = workflows;
        private readonly ICurrentUser _user = user;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // The ReferenceType and definition code this kind uses with the workflow engine.
        protected abstract string Reference { get; }
        protected abstract string Definition { get; }
        protected abstract ServiceRequestSummary Map(T request);

        public async Task<Result<ServiceRequestSummary>> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(requestId, tracking: false, Permissions.Requests.Read, cancellationToken);

            return loaded.TryGetValue(out var request) ? Map(request) : Result<ServiceRequestSummary>.Fail(loaded.Errors);
        }

        public async Task<Result<IReadOnlyList<ServiceRequestSummary>>> ListOfStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Requests.Read, studentId, hideRefusal: false, cancellationToken);
            if (!student.TryGetValue(out _))
            {
                return Result<IReadOnlyList<ServiceRequestSummary>>.Fail(student.Errors);
            }

            var items = await _requests.ListAsync(new ServiceRequestsOfStudentSpecification<T>(studentId), cancellationToken);

            return Result<IReadOnlyList<ServiceRequestSummary>>.Ok([.. items.Select(Map)]);
        }

        // Every student's requests of this kind: whole-university readers (the registrar).
        public async Task<Result<PagedResult<ServiceRequestSummary>>> ListAsync(PaginationQuery query, ServiceRequestStatus? status, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireUniversityWideAsync(Permissions.Requests.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<PagedResult<ServiceRequestSummary>>.Fail(permission.Errors);
            }

            var items = await _requests.ListAsync(new ServiceRequestsListSpecification<T>(query, status), cancellationToken);
            var total = await _requests.CountAsync(new ServiceRequestsCountSpecification<T>(status), cancellationToken);

            return PagedResult<ServiceRequestSummary>.Create([.. items.Select(Map)], query, total);
        }

        // The student withdraws a request that is still open; its workflow is cancelled with it.
        public async Task<Result<ServiceRequestSummary>> CancelAsync(Guid requestId, RequestInfo info, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(requestId, tracking: true, Permissions.Requests.Submit, cancellationToken);
            if (!loaded.TryGetValue(out var request))
            {
                return Result<ServiceRequestSummary>.Fail(loaded.Errors);
            }

            if (!request.IsOpen)
            {
                return ServiceRequestErrors.InvalidState;
            }

            if (request.WorkflowInstanceId is { } instanceId)
            {
                var stopped = await _workflows.CancelAsync(instanceId, null, info, cancellationToken);
                if (stopped.IsFailure)
                {
                    return Result<ServiceRequestSummary>.Fail(stopped.Errors);
                }
            }

            var previous = request.Status;
            var cancelled = request.Cancel(_user.UserId, _clock.UtcNow);
            if (cancelled.IsFailure)
            {
                return Result<ServiceRequestSummary>.Fail(cancelled.Errors);
            }

            _requests.Update(request);

            var entry = new AuditEntry(ServiceRequestAuditActions.Cancelled, _user.UserId, Reference, request.Id.ToString(), info.IpAddress, info.UserAgent,
                $"Status={previous}", $"Status={request.Status}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<ServiceRequestSummary>.Fail(committed.Errors) : Map(request);
        }

        // The student corrected a returned request. The workflow restarts at its first step; if a previous attempt already
        // restarted it (and only the save here failed), the workflow is not asked again.
        public async Task<Result<ServiceRequestSummary>> ResubmitAsync(Guid requestId, RequestInfo info, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(requestId, tracking: true, Permissions.Requests.Submit, cancellationToken);
            if (!loaded.TryGetValue(out var request))
            {
                return Result<ServiceRequestSummary>.Fail(loaded.Errors);
            }

            if (request.Status != ServiceRequestStatus.Returned)
            {
                return ServiceRequestErrors.InvalidState;
            }

            if (request.WorkflowInstanceId is { } instanceId)
            {
                var instance = await _workflows.GetAsync(instanceId, cancellationToken);
                if (!instance.TryGetValue(out var current))
                {
                    return Result<ServiceRequestSummary>.Fail(instance.Errors);
                }

                if (current.Status == WorkflowInstanceStatus.Returned)
                {
                    var restarted = await _workflows.ResubmitAsync(instanceId, null, info, cancellationToken);
                    if (restarted.IsFailure)
                    {
                        return Result<ServiceRequestSummary>.Fail(restarted.Errors);
                    }
                }
            }

            var resubmitted = request.Resubmit(_clock.UtcNow);
            if (resubmitted.IsFailure)
            {
                return Result<ServiceRequestSummary>.Fail(resubmitted.Errors);
            }

            _requests.Update(request);

            var entry = new AuditEntry(ServiceRequestAuditActions.Resubmitted, _user.UserId, Reference, request.Id.ToString(), info.IpAddress, info.UserAgent,
                $"Status={ServiceRequestStatus.Returned}", $"Status={request.Status}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<ServiceRequestSummary>.Fail(committed.Errors) : Map(request);
        }

        // The shared submit sequence. `create` builds the request; `charge` (optional) takes the fee and records it on the request.
        protected async Task<Result<ServiceRequestSummary>> SubmitCoreAsync(
            Guid studentId, Specification<T>? openOne, Func<Guid, DateTimeOffset, Result<T>> create,
            Func<T, CancellationToken, Task<Result>>? charge, RequestInfo info, CancellationToken cancellationToken,
            Func<Student, CancellationToken, Task<Result>>? validate = null)
        {
            if (_user.UserId is not { } userId)
            {
                return ServiceRequestErrors.ActorRequired;
            }

            var student = await _access.AuthorizeStudentAsync(Permissions.Requests.Submit, studentId, hideRefusal: false, cancellationToken);
            if (!student.TryGetValue(out var authorized))
            {
                return Result<ServiceRequestSummary>.Fail(student.Errors);
            }

            // The kind's own checks (the grade named by the request, for example).
            if (validate is not null)
            {
                var valid = await validate(authorized, cancellationToken);
                if (valid.IsFailure)
                {
                    return Result<ServiceRequestSummary>.Fail(valid.Errors);
                }
            }

            if (openOne is not null && await _requests.AnyAsync(openOne, cancellationToken))
            {
                return ServiceRequestErrors.AlreadyPending;
            }

            var created = create(userId, _clock.UtcNow);
            if (!created.TryGetValue(out var request))
            {
                return Result<ServiceRequestSummary>.Fail(created.Errors);
            }

            _requests.Add(request);

            var entry = new AuditEntry(ServiceRequestAuditActions.Submitted, userId, Reference, request.Id.ToString(), info.IpAddress, info.UserAgent,
                null, $"StudentId={studentId};Status={request.Status}");
            var saved = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ServiceRequestSummary>.Fail(saved.Errors);
            }

            if (charge is not null)
            {
                var charged = await charge(request, cancellationToken);
                if (charged.IsFailure)
                {
                    return await CompensateAsync(request, charged.Errors, userId, cancellationToken);
                }
            }

            var started = await _workflows.StartAsync(new StartWorkflowCommand(Definition, Reference, request.Id, studentId), info, cancellationToken);
            if (!started.TryGetValue(out var instance))
            {
                return await CompensateAsync(request, started.Errors, userId, cancellationToken);
            }

            var attached = request.AttachWorkflow(instance.Id);
            if (attached.IsFailure)
            {
                return Result<ServiceRequestSummary>.Fail(attached.Errors);
            }

            _requests.Update(request);

            var linked = await _unitOfWork.SaveChangesAsync(cancellationToken);

            return linked.IsFailure ? Result<ServiceRequestSummary>.Fail(linked.Errors) : Map(request);
        }

        private async Task<Result<ServiceRequestSummary>> CompensateAsync(T request, IReadOnlyList<Error> errors, Guid userId, CancellationToken cancellationToken)
        {
            if (request.Cancel(userId, _clock.UtcNow).IsSuccess)
            {
                _requests.Update(request);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result<ServiceRequestSummary>.Fail(errors);
        }

        // Load, then the scope check. Without the permission, or outside the scope, the request is "not found".
        private async Task<Result<T>> LoadAsync(Guid requestId, bool tracking, string permission, CancellationToken cancellationToken)
        {
            var request = await _requests.FirstOrDefaultAsync(new ServiceRequestByIdSpecification<T>(requestId, tracking), cancellationToken);
            if (request is null)
            {
                return ServiceRequestErrors.NotFound;
            }

            var scope = await _access.AuthorizeStudentAsync(permission, request.StudentId, hideRefusal: true, cancellationToken);

            return scope.IsFailure ? Result<T>.Fail(scope.Errors) : request;
        }
    }
}
