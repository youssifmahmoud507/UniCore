using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    public sealed class WorkflowService(IPermissionAuthorizer authorizer, ICurrentUser user, WorkflowApproverCheck approvers, IAggregateRepository<WorkflowDefinition> definitions, IAggregateRepository<WorkflowInstance> instances, IAggregateRepository<Student> students, IRepository<AcademicProgram> programs, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit) : IWorkflowService
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly WorkflowApproverCheck _approvers = approvers;
        private readonly IAggregateRepository<WorkflowDefinition> _definitions = definitions;
        private readonly IAggregateRepository<WorkflowInstance> _instances = instances;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // ---------- start ----------

        public async Task<Result<WorkflowInstanceDto>> StartAsync(StartWorkflowCommand command, RequestInfo? request = null, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            // The requester owns the request, so a student passes with Requests.Submit.
            var auth = await _authorizer.AuthorizeAsync(Permissions.Requests.Submit, ResourceContext.OwnedBy(userId), cancellationToken);
            if (auth.IsFailure)
            {
                return Result<WorkflowInstanceDto>.Fail(auth.Errors);
            }

            var code = WorkflowDefinition.NormalizeCode(command.DefinitionCode);
            var referenceType = command.ReferenceType?.Trim();

            if (code is null || string.IsNullOrEmpty(referenceType) || command.ReferenceId is not { } referenceId)
            {
                return WorkflowErrors.RequestDataInvalid;
            }

            var definition = await _definitions.FirstOrDefaultAsync(new ActiveWorkflowDefinitionByCodeSpecification(code), cancellationToken);
            if (definition is null)
            {
                var known = await _definitions.AnyAsync(new WorkflowDefinitionsByCodeSpecification(code), cancellationToken);

                return known ? WorkflowErrors.DefinitionInactive : WorkflowErrors.DefinitionNotFound;
            }

            if (await _instances.AnyAsync(new ActiveInstanceForReferenceSpecification(referenceType, referenceId), cancellationToken))
            {
                return WorkflowErrors.AlreadyActive;
            }

            var scopeType = command.ScopeType;
            var scopeId = command.ScopeId;

            if (command.SubjectStudentId is { } studentId)
            {
                var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);
                if (student is null)
                {
                    return StudentErrors.NotFound;
                }

                // No scope given: the request sits in the department of the student's program.
                if (scopeType is null && scopeId is null && student.CurrentProgram is { } current)
                {
                    var program = await _programs.GetByIdAsync(current.AcademicProgramId, cancellationToken);
                    if (program is not null)
                    {
                        scopeType = ScopeType.Department;
                        scopeId = program.DepartmentId;
                    }
                }
            }

            var started = WorkflowInstance.Start(definition, userId, referenceType, referenceId, command.SubjectStudentId, scopeType, scopeId, _clock.UtcNow);
            if (!started.TryGetValue(out var instance))
            {
                return Result<WorkflowInstanceDto>.Fail(started.Errors);
            }

            _instances.Add(instance);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // Two starts for the same request at the same time: the unique index lets one win.
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? WorkflowErrors.AlreadyActive
                    : Result<WorkflowInstanceDto>.Fail(saved.Errors);
            }

            await WriteAuditAsync(WorkflowAuditActions.Started, userId, instance, request, null, $"{instance.DefinitionCode} v{instance.DefinitionVersion} for {instance.ReferenceType}", cancellationToken);

            return WorkflowMapper.ToDto(instance);
        }

        // ---------- decisions ----------

        public Task<Result<WorkflowInstanceDto>> ApproveAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
            => DecideAsync(instanceId, comment, request, WorkflowDecision.Approved, cancellationToken);

        public Task<Result<WorkflowInstanceDto>> RejectAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
            => DecideAsync(instanceId, comment, request, WorkflowDecision.Rejected, cancellationToken);

        public Task<Result<WorkflowInstanceDto>> ReturnAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
            => DecideAsync(instanceId, comment, request, WorkflowDecision.Returned, cancellationToken);

        private async Task<Result<WorkflowInstanceDto>> DecideAsync(Guid instanceId, string? comment, RequestInfo? request, WorkflowDecision decision, CancellationToken cancellationToken)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var loaded = await LoadAsync(instanceId, cancellationToken);
            if (!loaded.TryGetValue(out var pair))
            {
                return Result<WorkflowInstanceDto>.Fail(loaded.Errors);
            }

            var (instance, definition) = pair;

            // A finished (or returned) instance accepts no decision, whoever asks.
            if (instance.Status != WorkflowInstanceStatus.InProgress || instance.CurrentRole is not { } role)
            {
                return WorkflowErrors.InvalidState;
            }

            if (!await _approvers.CanActAsync(role, instance.ScopeType, instance.ScopeId, cancellationToken))
            {
                return WorkflowErrors.NotCurrentApprover;
            }

            var now = _clock.UtcNow;

            var done = decision switch
            {
                WorkflowDecision.Approved => instance.Approve(definition, userId, comment, now),
                WorkflowDecision.Rejected => instance.Reject(definition, userId, comment, now),
                _ => instance.Return(definition, userId, comment, now)
            };

            if (done.IsFailure)
            {
                return Result<WorkflowInstanceDto>.Fail(done.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<WorkflowInstanceDto>.Fail(saved.Errors);
            }

            var action = decision switch
            {
                WorkflowDecision.Approved => WorkflowAuditActions.Approved,
                WorkflowDecision.Rejected => WorkflowAuditActions.Rejected,
                _ => WorkflowAuditActions.Returned
            };

            await WriteAuditAsync(action, userId, instance, request, role, $"{instance.Status} step {instance.CurrentStepOrder}/{instance.TotalSteps}", cancellationToken);

            return WorkflowMapper.ToDto(instance);
        }

        // ---------- requester actions ----------

        public async Task<Result<WorkflowInstanceDto>> ResubmitAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var loaded = await LoadAsync(instanceId, cancellationToken);
            if (!loaded.TryGetValue(out var pair))
            {
                return Result<WorkflowInstanceDto>.Fail(loaded.Errors);
            }

            var (instance, definition) = pair;

            // A definition deactivated while the request waited for correction cannot restart.
            if (!definition.IsActive && instance.Status == WorkflowInstanceStatus.Returned)
            {
                return WorkflowErrors.DefinitionInactive;
            }

            var done = instance.Resubmit(definition, userId, comment, _clock.UtcNow);
            if (done.IsFailure)
            {
                return Result<WorkflowInstanceDto>.Fail(done.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<WorkflowInstanceDto>.Fail(saved.Errors);
            }

            await WriteAuditAsync(WorkflowAuditActions.Resubmitted, userId, instance, request, WorkflowInstanceStatus.Returned.ToString(), "restarted at step 1", cancellationToken);

            return WorkflowMapper.ToDto(instance);
        }

        // The requester, or an admin (Requests.Process with a Global scope), stops it while it is not finished.
        public async Task<Result<WorkflowInstanceDto>> CancelAsync(Guid instanceId, string? comment, RequestInfo? request = null, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var instance = await _instances.FirstOrDefaultAsync(new WorkflowInstanceByIdSpecification(instanceId, tracking: true), cancellationToken);
            if (instance is null)
            {
                return WorkflowErrors.InstanceNotFound;
            }

            var byAdmin = instance.RequesterUserId != userId
                && (await _authorizer.AuthorizeAsync(Permissions.Requests.Process, GlobalResource, cancellationToken)).IsSuccess;

            var previous = instance.Status;

            var done = instance.Cancel(userId, byAdmin, comment, _clock.UtcNow);
            if (done.IsFailure)
            {
                return Result<WorkflowInstanceDto>.Fail(done.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<WorkflowInstanceDto>.Fail(saved.Errors);
            }

            await WriteAuditAsync(WorkflowAuditActions.Cancelled, userId, instance, request, previous.ToString(), byAdmin ? "cancelled by an admin" : "cancelled by the requester", cancellationToken);

            return WorkflowMapper.ToDto(instance);
        }

        // ---------- read ----------

        // The requester reads their own; staff read what their scope covers (Requests.Read).
        public async Task<Result<WorkflowInstanceDto>> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
        {
            var instance = await _instances.FirstOrDefaultAsync(new WorkflowInstanceByIdSpecification(instanceId), cancellationToken);
            if (instance is null)
            {
                return WorkflowErrors.InstanceNotFound;
            }

            var resource = new ResourceContext(instance.ScopeType, instance.ScopeId, [instance.RequesterUserId]);

            var auth = await _authorizer.AuthorizeAsync(Permissions.Requests.Read, resource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<WorkflowInstanceDto>.Fail(auth.Errors);
            }

            return WorkflowMapper.ToDto(instance);
        }

        // ---------- helpers ----------

        private async Task<Result<(WorkflowInstance Instance, WorkflowDefinition Definition)>> LoadAsync(Guid instanceId, CancellationToken cancellationToken)
        {
            var instance = await _instances.FirstOrDefaultAsync(new WorkflowInstanceByIdSpecification(instanceId, tracking: true), cancellationToken);
            if (instance is null)
            {
                return WorkflowErrors.InstanceNotFound;
            }

            // The version the instance started with, even if a newer version is active now.
            var definition = await _definitions.FirstOrDefaultAsync(new WorkflowDefinitionByIdSpecification(instance.WorkflowDefinitionId), cancellationToken);
            if (definition is null)
            {
                return WorkflowErrors.DefinitionNotFound;
            }

            return (instance, definition);
        }

        private Task WriteAuditAsync(string action, Guid userId, WorkflowInstance instance, RequestInfo? request, string? oldValues, string? newValues, CancellationToken cancellationToken)
            => _audit.WriteAsync(new AuditEntry(action, userId, "WorkflowInstance", instance.Id.ToString(), request?.IpAddress, request?.UserAgent, oldValues, newValues), cancellationToken);
    }



}
