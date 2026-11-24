using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Registrations;
using UniCore.Application.Modules.StudentRequests.Specifications;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.StudentRequests;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.StudentRequests
{
    // What happens to a withdrawal or leave request when its workflow ends. Delivery is at-least-once, so every handler does
    // nothing when the request is no longer pending. Events of other workflows are ignored.
    public sealed class StudentRequestWorkflowHandlers(
        IAggregateRepository<WithdrawalRequest> withdrawals, IAggregateRepository<LeaveRequest> leaves, IAggregateRepository<WorkflowInstance> instances,
        IAggregateRepository<Student> students, RegistrationHandler registrations, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly RequestInfo Origin = new(null, "student-request-workflow");

        private readonly IAggregateRepository<WithdrawalRequest> _withdrawals = withdrawals;
        private readonly IAggregateRepository<LeaveRequest> _leaves = leaves;
        private readonly IAggregateRepository<WorkflowInstance> _instances = instances;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly RegistrationHandler _registrations = registrations;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static bool IsWithdrawal(string type) => string.Equals(type, StudentRequestHandler.WithdrawalReferenceType, StringComparison.Ordinal);

        private static bool IsLeave(string type) => string.Equals(type, StudentRequestHandler.LeaveReferenceType, StringComparison.Ordinal);

        private static bool IsPermanent(Error? error) => error?.ErrorType is ErrorType.Validation or ErrorType.NotFound or ErrorType.Conflict or ErrorType.BusinessRule or ErrorType.Forbidden or ErrorType.Unauthorized or ErrorType.InvalidCredentials;

        private Task<Result> CommitAsync(string action, Guid? actor, string entity, StudentRequest request, string? newValues, CancellationToken cancellationToken)
            => ChangeSupport.CommitAsync(_unitOfWork, _audit, new AuditEntry(action, actor, entity, request.Id.ToString(), null, Origin.UserAgent, null, newValues), cancellationToken);

        private async Task<Guid?> LastApproverAsync(Guid instanceId, CancellationToken cancellationToken)
        {
            var instance = await _instances.FirstOrDefaultAsync(new WorkflowInstanceByIdSpecification(instanceId), cancellationToken);

            return instance?.Approvals.Where(a => a.Decision == WorkflowDecision.Approved).OrderByDescending(a => a.DecidedAt).Select(a => (Guid?)a.ApproverUserId).FirstOrDefault();
        }

        public async Task<Result> CompletedAsync(WorkflowCompleted domainEvent, CancellationToken cancellationToken)
        {
            if (IsWithdrawal(domainEvent.ReferenceType))
            {
                return await ApplyWithdrawalAsync(domainEvent, cancellationToken);
            }

            return IsLeave(domainEvent.ReferenceType) ? await ApplyLeaveAsync(domainEvent, cancellationToken) : Result.Ok();
        }

        private async Task<Result> ApplyWithdrawalAsync(WorkflowCompleted domainEvent, CancellationToken cancellationToken)
        {
            var found = await _withdrawals.FirstOrDefaultAsync(new WithdrawalByIdSpecification(domainEvent.ReferenceId, tracking: true), cancellationToken);
            if (found is not { Status: AcademicRequestStatus.Pending } withdrawal)
            {
                return Result.Ok();
            }

            if (await LastApproverAsync(domainEvent.InstanceId, cancellationToken) is not { } approvedBy)
            {
                return Result.Fail(StudentRequestErrors.WithdrawalNotFound);
            }

            var applied = await _registrations.WithdrawApprovedAsync(withdrawal.RegistrationId, withdrawal.EnrollmentId, approvedBy, Origin, cancellationToken);
            if (applied.IsFailure)
            {
                // Retry only what may work next time (a concurrent change). A refused withdrawal will be refused again: the request
                // is marked Failed so the student and the registrar can see it, and the event is done.
                if (!IsPermanent(applied.Error))
                {
                    return Result.Fail(applied.Errors);
                }

                if (withdrawal.MarkFailed(applied.Error?.Code ?? "WITHDRAWAL_FAILED", _clock.UtcNow).IsFailure)
                {
                    return Result.Ok();
                }

                return await CommitAsync(StudentRequestAuditActions.WithdrawalFailed, approvedBy, "WithdrawalRequest", withdrawal, $"Failed={withdrawal.FailureCode}", cancellationToken);
            }

            if (withdrawal.MarkApplied(approvedBy, _clock.UtcNow).IsFailure)
            {
                return Result.Ok();
            }

            return await CommitAsync(StudentRequestAuditActions.WithdrawalApplied, approvedBy, "WithdrawalRequest", withdrawal, $"Enrollment={withdrawal.EnrollmentId}", cancellationToken);
        }

        private async Task<Result> ApplyLeaveAsync(WorkflowCompleted domainEvent, CancellationToken cancellationToken)
        {
            var found = await _leaves.FirstOrDefaultAsync(new LeaveByIdSpecification(domainEvent.ReferenceId, tracking: true), cancellationToken);
            if (found is not { Status: AcademicRequestStatus.Pending } leave)
            {
                return Result.Ok();
            }

            if (await LastApproverAsync(domainEvent.InstanceId, cancellationToken) is not { } approvedBy)
            {
                return Result.Fail(StudentRequestErrors.LeaveNotFound);
            }

            var failure = await ChangeStatusAsync(leave, approvedBy, cancellationToken);
            if (failure is not null)
            {
                if (!IsPermanent(failure.Error))
                {
                    return Result.Fail(failure.Errors);
                }

                if (leave.MarkFailed(failure.Error?.Code ?? "LEAVE_FAILED", _clock.UtcNow).IsFailure)
                {
                    return Result.Ok();
                }

                return await CommitAsync(StudentRequestAuditActions.LeaveFailed, approvedBy, "LeaveRequest", leave, $"Failed={leave.FailureCode}", cancellationToken);
            }

            if (leave.MarkApplied(approvedBy, _clock.UtcNow).IsFailure)
            {
                return Result.Ok();
            }

            return await CommitAsync(StudentRequestAuditActions.LeaveApplied, approvedBy, "LeaveRequest", leave, $"OnLeave from {_clock.UtcToday:yyyy-MM-dd}; return {leave.ExpectedReturnDate:yyyy-MM-dd}", cancellationToken);
        }

        // Active -> OnLeave, effective today, by the last approver. Returns null on success, the failure otherwise.
        private async Task<Result?> ChangeStatusAsync(LeaveRequest leave, Guid approvedBy, CancellationToken cancellationToken)
        {
            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(leave.StudentId, tracking: true), cancellationToken);
            if (student is null)
            {
                return Result.Fail(StudentErrors.NotFound);
            }

            var oldStatus = student.AcademicStatus;
            var changed = student.ChangeStatus(StudentAcademicStatus.OnLeave, _clock.UtcToday, $"Leave request: {leave.Reason}", approvedBy, _clock.UtcToday, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return changed;
            }

            var entry = new AuditEntry(AuditActions.StudentStatusChanged, approvedBy, "Student", student.Id.ToString(), null, Origin.UserAgent, oldStatus.ToString(), $"{student.AcademicStatus} (leave request {leave.Id})");
            var saved = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return saved.IsFailure ? saved : null;
        }

        public async Task<Result> RejectedAsync(WorkflowRejected domainEvent, CancellationToken cancellationToken)
        {
            if (IsWithdrawal(domainEvent.ReferenceType))
            {
                var found = await _withdrawals.FirstOrDefaultAsync(new WithdrawalByIdSpecification(domainEvent.ReferenceId, tracking: true), cancellationToken);

                return found is { Status: AcademicRequestStatus.Pending } w && w.MarkRejected(_clock.UtcNow).IsSuccess
                    ? await CommitAsync(StudentRequestAuditActions.WithdrawalRejected, domainEvent.DecidedByUserId, "WithdrawalRequest", w, $"Comment={domainEvent.Comment}", cancellationToken)
                    : Result.Ok();
            }

            if (IsLeave(domainEvent.ReferenceType))
            {
                var found = await _leaves.FirstOrDefaultAsync(new LeaveByIdSpecification(domainEvent.ReferenceId, tracking: true), cancellationToken);

                return found is { Status: AcademicRequestStatus.Pending } l && l.MarkRejected(_clock.UtcNow).IsSuccess
                    ? await CommitAsync(StudentRequestAuditActions.LeaveRejected, domainEvent.DecidedByUserId, "LeaveRequest", l, $"Comment={domainEvent.Comment}", cancellationToken)
                    : Result.Ok();
            }

            return Result.Ok();
        }

        public async Task<Result> CancelledAsync(WorkflowCancelled domainEvent, CancellationToken cancellationToken)
        {
            if (IsWithdrawal(domainEvent.ReferenceType))
            {
                var found = await _withdrawals.FirstOrDefaultAsync(new WithdrawalByIdSpecification(domainEvent.ReferenceId, tracking: true), cancellationToken);

                return found is { Status: AcademicRequestStatus.Pending } w && w.MarkCancelled(_clock.UtcNow).IsSuccess
                    ? await CommitAsync(StudentRequestAuditActions.WithdrawalCancelled, domainEvent.CancelledByUserId, "WithdrawalRequest", w, null, cancellationToken)
                    : Result.Ok();
            }

            if (IsLeave(domainEvent.ReferenceType))
            {
                var found = await _leaves.FirstOrDefaultAsync(new LeaveByIdSpecification(domainEvent.ReferenceId, tracking: true), cancellationToken);

                return found is { Status: AcademicRequestStatus.Pending } l && l.MarkCancelled(_clock.UtcNow).IsSuccess
                    ? await CommitAsync(StudentRequestAuditActions.LeaveCancelled, domainEvent.CancelledByUserId, "LeaveRequest", l, null, cancellationToken)
                    : Result.Ok();
            }

            return Result.Ok();
        }
    }

    public sealed class StudentRequestWorkflowCompletedHandler(StudentRequestWorkflowHandlers handlers) : IDomainEventHandler<WorkflowCompleted>
    {
        public Task<Result> HandleAsync(WorkflowCompleted domainEvent, CancellationToken cancellationToken = default) => handlers.CompletedAsync(domainEvent, cancellationToken);
    }

    public sealed class StudentRequestWorkflowRejectedHandler(StudentRequestWorkflowHandlers handlers) : IDomainEventHandler<WorkflowRejected>
    {
        public Task<Result> HandleAsync(WorkflowRejected domainEvent, CancellationToken cancellationToken = default) => handlers.RejectedAsync(domainEvent, cancellationToken);
    }

    public sealed class StudentRequestWorkflowCancelledHandler(StudentRequestWorkflowHandlers handlers) : IDomainEventHandler<WorkflowCancelled>
    {
        public Task<Result> HandleAsync(WorkflowCancelled domainEvent, CancellationToken cancellationToken = default) => handlers.CancelledAsync(domainEvent, cancellationToken);
    }
}
