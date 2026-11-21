using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Calendar;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.Graduation.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Graduation;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Graduation
{
    // The registrar's decision (Graduation.Decide with a whole-university scope).
    // Approval: the application is Submitted, no financial hold blocks Graduation, the student is still eligible NOW (the figures are
    // read again, not taken from the application), then the student becomes Graduated and GraduationApproved is raised, all in one save.
    public sealed class GraduationDecisionHandler(
        GraduationAccess access, IGraduationEligibilityService eligibility, IFinancialEligibilityService finance,
        IAggregateRepository<GraduationApplication> applications, IAggregateRepository<Student> students,
        ICurrentUser user, IAcademicClock academicClock, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly GraduationAccess _access = access;
        private readonly IGraduationEligibilityService _eligibility = eligibility;
        private readonly IFinancialEligibilityService _finance = finance;
        private readonly IAggregateRepository<GraduationApplication> _applications = applications;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly ICurrentUser _user = user;
        private readonly IAcademicClock _academicClock = academicClock;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<GraduationApplicationSummary>> ApproveAsync(Guid applicationId, string? note, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForDecisionAsync(applicationId, cancellationToken);
            if (!loaded.TryGetValue(out var application))
            {
                return Result<GraduationApplicationSummary>.Fail(loaded.Errors);
            }

            if (_user.UserId is not { } approver)
            {
                return GraduationErrors.ActorRequired;
            }

            // Any financial hold on Graduation stops the decision.
            var held = await _finance.HasBlockingHoldAsync(application.StudentId, FinancialHoldArea.Graduation, cancellationToken);
            if (!held.TryGetValue(out var blocked))
            {
                return Result<GraduationApplicationSummary>.Fail(held.Errors);
            }

            if (blocked)
            {
                return FinanceErrors.FinancialHold;
            }

            var check = await _eligibility.CheckAsync(application.StudentId, cancellationToken);
            if (!check.TryGetValue(out var eligibility))
            {
                return Result<GraduationApplicationSummary>.Fail(check.Errors);
            }

            if (!eligibility.IsEligible)
            {
                return Result<GraduationApplicationSummary>.Fail(eligibility.Unmet.Count > 0 ? eligibility.Unmet : [GraduationErrors.NotEligible]);
            }

            var student = await _students.FirstOrDefaultAsync(new UniCore.Application.Modules.Students.Specifications.StudentByIdSpecification(application.StudentId, tracking: true), cancellationToken);
            if (student is null)
            {
                return GraduationErrors.StudentNotFound;
            }

            var today = _academicClock.Today;
            var now = _clock.UtcNow;

            var graduated = student.ChangeStatus(StudentAcademicStatus.Graduated, today, null, approver, today, now);
            if (graduated.IsFailure)
            {
                return Result<GraduationApplicationSummary>.Fail(graduated.Errors);
            }

            var approved = application.Approve(approver, eligibility.Cumulative.Gpa, eligibility.Cumulative.EarnedCredits, eligibility.Cumulative.Standing, today, note, now);
            if (approved.IsFailure)
            {
                return Result<GraduationApplicationSummary>.Fail(approved.Errors);
            }

            _students.Update(student);
            _applications.Update(application);

            var entry = new AuditEntry(GraduationAuditActions.Approved, approver, "GraduationApplication", application.Id.ToString(), request.IpAddress, request.UserAgent,
                $"Status={GraduationApplicationStatus.Submitted}",
                $"Status={application.Status};StudentId={application.StudentId};Gpa={eligibility.Cumulative.Gpa};Credits={eligibility.Cumulative.EarnedCredits}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<GraduationApplicationSummary>.Fail(committed.Errors) : GraduationMapper.ToSummary(application);
        }

        public async Task<Result<GraduationApplicationSummary>> RejectAsync(Guid applicationId, string? reason, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForDecisionAsync(applicationId, cancellationToken);
            if (!loaded.TryGetValue(out var application))
            {
                return Result<GraduationApplicationSummary>.Fail(loaded.Errors);
            }

            if (_user.UserId is not { } decider)
            {
                return GraduationErrors.ActorRequired;
            }

            var rejected = application.Reject(decider, reason, _clock.UtcNow);
            if (rejected.IsFailure)
            {
                return Result<GraduationApplicationSummary>.Fail(rejected.Errors);
            }

            _applications.Update(application);

            var entry = new AuditEntry(GraduationAuditActions.Rejected, decider, "GraduationApplication", application.Id.ToString(), request.IpAddress, request.UserAgent,
                $"Status={GraduationApplicationStatus.Submitted}", $"Status={application.Status};Reason={application.DecisionNote}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<GraduationApplicationSummary>.Fail(committed.Errors) : GraduationMapper.ToSummary(application);
        }

        // permission alone -> load -> whole-university scope -> state (Submitted).
        private async Task<Result<GraduationApplication>> LoadForDecisionAsync(Guid applicationId, CancellationToken cancellationToken)
        {
            var permission = await _access.RequireAsync(Permissions.Graduation.Decide, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<GraduationApplication>.Fail(permission.Errors);
            }

            var application = await _applications.FirstOrDefaultAsync(new GraduationApplicationByIdSpecification(applicationId, tracking: true), cancellationToken);
            if (application is null)
            {
                return GraduationErrors.ApplicationNotFound;
            }

            var scope = await _access.RequireUniversityWideAsync(Permissions.Graduation.Decide, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<GraduationApplication>.Fail(scope.Errors);
            }

            return application.IsActive ? application : GraduationErrors.InvalidState;
        }
    }
}
