using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Calendar;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Registrations.Specifications;
using UniCore.Application.Modules.StudentRequests.Specifications;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.StudentRequests;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.StudentRequests
{
    // A student asks to withdraw from a course or to take a leave of absence. The student must be the owner of the record
    // (Requests.Submit, the same grant that starts any workflow). The request starts its workflow (WITHDRAWAL_REQUEST or
    // LEAVE_REQUEST); when the workflow completes, StudentRequestWorkflowHandlers apply it.
    public sealed class StudentRequestHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Student> students, IAggregateRepository<Registration> registrations,
        IAggregateRepository<WithdrawalRequest> withdrawals, IAggregateRepository<LeaveRequest> leaves,
        IAcademicCalendarService calendar, IWorkflowService workflows, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        public const string WithdrawalWorkflowCode = "WITHDRAWAL_REQUEST";
        public const string WithdrawalReferenceType = "WithdrawalRequest";
        public const string LeaveWorkflowCode = "LEAVE_REQUEST";
        public const string LeaveReferenceType = "LeaveRequest";

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IAggregateRepository<Registration> _registrations = registrations;
        private readonly IAggregateRepository<WithdrawalRequest> _withdrawals = withdrawals;
        private readonly IAggregateRepository<LeaveRequest> _leaves = leaves;
        private readonly IAcademicCalendarService _calendar = calendar;
        private readonly IWorkflowService _workflows = workflows;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // Permission alone first, then the student's own record.
        private async Task<Result<(Guid UserId, Student Student)>> OwnStudentAsync(Guid studentId, CancellationToken cancellationToken)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var allowed = await _authorizer.AuthorizeAsync(Permissions.Requests.Submit, ResourceContext.OwnedBy(userId), cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<(Guid, Student)>.Fail(allowed.Errors);
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);
            if (student is null || student.UserId != userId)
            {
                // Someone else's record looks the same as a missing one.
                return StudentRequestErrors.NotYourRequest;
            }

            return (userId, student);
        }

        // ---------- withdrawal ----------

        public async Task<Result<WithdrawalRequestDto>> SubmitWithdrawalAsync(SubmitWithdrawalCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (command.RegistrationId is not { } registrationId || registrationId == Guid.Empty || command.EnrollmentId is not { } enrollmentId || enrollmentId == Guid.Empty)
            {
                return StudentRequestErrors.WithdrawalDataInvalid;
            }

            var registration = await _registrations.FirstOrDefaultAsync(new RegistrationByIdSpecification(registrationId), cancellationToken);
            if (registration is null)
            {
                return RegistrationsErrors.RegistrationNotFound;
            }

            var owned = await OwnStudentAsync(registration.StudentId, cancellationToken);
            if (!owned.TryGetValue(out var owner))
            {
                return Result<WithdrawalRequestDto>.Fail(owned.Errors);
            }

            var enrollment = registration.Enrollments.FirstOrDefault(e => e.Id == enrollmentId);
            if (enrollment is null)
            {
                return RegistrationsErrors.EnrollmentNotFound;
            }

            if (!registration.AcceptsChanges)
            {
                return RegistrationsErrors.InvalidState;
            }

            if (enrollment.Status != EnrollmentStatus.Registered)
            {
                return RegistrationsErrors.EnrollmentInvalidState;
            }

            if (owner.Student.AcademicStatus != StudentAcademicStatus.Active)
            {
                return RegistrationsErrors.StudentInactive;
            }

            var open = await _calendar.IsWithdrawalOpenAsync(registration.AcademicSemesterId, cancellationToken);
            if (!open.TryGetValue(out var isOpen))
            {
                return Result<WithdrawalRequestDto>.Fail(open.Errors);
            }

            if (!isOpen)
            {
                return RegistrationsErrors.WithdrawalDeadlinePassed;
            }

            if (await _withdrawals.AnyAsync(new PendingWithdrawalOfEnrollmentSpecification(enrollmentId), cancellationToken))
            {
                return StudentRequestErrors.WithdrawalAlreadyPending;
            }

            var created = WithdrawalRequest.Create(registration.StudentId, registrationId, enrollmentId, enrollment.CourseSectionId, command.Reason, owner.UserId, _clock.UtcNow);
            if (!created.TryGetValue(out var withdrawal))
            {
                return Result<WithdrawalRequestDto>.Fail(created.Errors);
            }

            _withdrawals.Add(withdrawal);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // The filtered unique index lets one of two parallel requests win.
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code ? StudentRequestErrors.WithdrawalAlreadyPending : Result<WithdrawalRequestDto>.Fail(saved.Errors);
            }

            var started = await _workflows.StartAuthorizedAsync(new StartWorkflowCommand(WithdrawalWorkflowCode, WithdrawalReferenceType, withdrawal.Id, registration.StudentId), request, cancellationToken);

            var tracked = await _withdrawals.FirstOrDefaultAsync(new WithdrawalByIdSpecification(withdrawal.Id, tracking: true), cancellationToken);
            if (tracked is null)
            {
                return StudentRequestErrors.WithdrawalNotFound;
            }

            if (!started.TryGetValue(out var instance))
            {
                // No workflow means nobody can approve it: close the request so it does not block a new one.
                if (tracked.MarkCancelled(_clock.UtcNow).IsSuccess)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return Result<WithdrawalRequestDto>.Fail(started.Errors);
            }

            var attached = tracked.AttachWorkflow(instance.Id);
            if (attached.IsFailure)
            {
                return Result<WithdrawalRequestDto>.Fail(attached.Errors);
            }

            var entry = new AuditEntry(StudentRequestAuditActions.WithdrawalRequested, owner.UserId, "WithdrawalRequest", tracked.Id.ToString(), request.IpAddress, request.UserAgent, null, $"Enrollment={enrollmentId};Reason={tracked.Reason}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<WithdrawalRequestDto>.Fail(committed.Errors) : StudentRequestMapper.ToDto(tracked);
        }

        public async Task<Result<WithdrawalRequestDto>> GetWithdrawalAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var found = await _withdrawals.FirstOrDefaultAsync(new WithdrawalByIdSpecification(id), cancellationToken);
            if (found is null)
            {
                return StudentRequestErrors.WithdrawalNotFound;
            }

            var allowed = await CanReadAsync(found, cancellationToken);

            return allowed ? StudentRequestMapper.ToDto(found) : StudentRequestErrors.WithdrawalNotFound;
        }

        public async Task<Result<IReadOnlyList<WithdrawalRequestDto>>> MyWithdrawalsAsync(CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var items = await _withdrawals.ListAsync(new WithdrawalsOfRequesterSpecification(userId), cancellationToken);

            return items.Select(StudentRequestMapper.ToDto).ToList();
        }

        // ---------- leave ----------

        public async Task<Result<LeaveRequestDto>> SubmitLeaveAsync(SubmitLeaveCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (command.StudentId is not { } studentId || studentId == Guid.Empty || command.StartDate is not { } start || command.ExpectedReturnDate is not { } back)
            {
                return StudentRequestErrors.LeaveDataInvalid;
            }

            var owned = await OwnStudentAsync(studentId, cancellationToken);
            if (!owned.TryGetValue(out var owner))
            {
                return Result<LeaveRequestDto>.Fail(owned.Errors);
            }

            if (start < _clock.UtcToday)
            {
                return StudentRequestErrors.LeaveStartInPast;
            }

            if (owner.Student.AcademicStatus != StudentAcademicStatus.Active)
            {
                return RegistrationsErrors.StudentInactive;
            }

            if (await _leaves.AnyAsync(new PendingLeaveOfStudentSpecification(studentId), cancellationToken))
            {
                return StudentRequestErrors.LeaveAlreadyPending;
            }

            var created = LeaveRequest.Create(studentId, start, back, command.Reason, owner.UserId, _clock.UtcNow);
            if (!created.TryGetValue(out var leave))
            {
                return Result<LeaveRequestDto>.Fail(created.Errors);
            }

            _leaves.Add(leave);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code ? StudentRequestErrors.LeaveAlreadyPending : Result<LeaveRequestDto>.Fail(saved.Errors);
            }

            var started = await _workflows.StartAuthorizedAsync(new StartWorkflowCommand(LeaveWorkflowCode, LeaveReferenceType, leave.Id, studentId), request, cancellationToken);

            var tracked = await _leaves.FirstOrDefaultAsync(new LeaveByIdSpecification(leave.Id, tracking: true), cancellationToken);
            if (tracked is null)
            {
                return StudentRequestErrors.LeaveNotFound;
            }

            if (!started.TryGetValue(out var instance))
            {
                if (tracked.MarkCancelled(_clock.UtcNow).IsSuccess)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return Result<LeaveRequestDto>.Fail(started.Errors);
            }

            var attached = tracked.AttachWorkflow(instance.Id);
            if (attached.IsFailure)
            {
                return Result<LeaveRequestDto>.Fail(attached.Errors);
            }

            var entry = new AuditEntry(StudentRequestAuditActions.LeaveRequested, owner.UserId, "LeaveRequest", tracked.Id.ToString(), request.IpAddress, request.UserAgent, null, $"From={tracked.StartDate:yyyy-MM-dd};Return={tracked.ExpectedReturnDate:yyyy-MM-dd};Reason={tracked.Reason}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<LeaveRequestDto>.Fail(committed.Errors) : StudentRequestMapper.ToDto(tracked);
        }

        public async Task<Result<LeaveRequestDto>> GetLeaveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var found = await _leaves.FirstOrDefaultAsync(new LeaveByIdSpecification(id), cancellationToken);
            if (found is null)
            {
                return StudentRequestErrors.LeaveNotFound;
            }

            var allowed = await CanReadAsync(found, cancellationToken);

            return allowed ? StudentRequestMapper.ToDto(found) : StudentRequestErrors.LeaveNotFound;
        }

        public async Task<Result<IReadOnlyList<LeaveRequestDto>>> MyLeavesAsync(CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var items = await _leaves.ListAsync(new LeavesOfRequesterSpecification(userId), cancellationToken);

            return items.Select(StudentRequestMapper.ToDto).ToList();
        }

        // The student who asked, or registration staff. Anyone else gets "not found".
        private async Task<bool> CanReadAsync(StudentRequest found, CancellationToken cancellationToken)
        {
            if (!_user.IsAuthenticated)
            {
                return false;
            }

            if (_user.UserId == found.RequestedByUserId)
            {
                return true;
            }

            return (await _authorizer.AuthorizeAsync(Permissions.Registration.Manage, null, cancellationToken)).IsSuccess;
        }
    }
}
