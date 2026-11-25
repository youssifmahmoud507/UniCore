using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Graduation.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Graduation;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Graduation
{
    // What the student (and staff reading) do with graduation applications. Order everywhere: permission alone -> load -> scope -> state -> change.
    public sealed class GraduationApplicationHandler(
        GraduationAccess access, IGraduationEligibilityService eligibility, IAggregateRepository<GraduationApplication> applications,
        ICurrentUser user, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly GraduationAccess _access = access;
        private readonly IGraduationEligibilityService _eligibility = eligibility;
        private readonly IAggregateRepository<GraduationApplication> _applications = applications;
        private readonly ICurrentUser _user = user;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<GraduationEligibilitySummary>> GetEligibilityAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Graduation.Read, studentId, cancellationToken);
            if (!student.TryGetValue(out _))
            {
                return Result<GraduationEligibilitySummary>.Fail(student.Errors);
            }

            var check = await _eligibility.CheckAsync(studentId, cancellationToken);

            return check.TryGetValue(out var eligibility) ? GraduationMapper.ToSummary(eligibility) : Result<GraduationEligibilitySummary>.Fail(check.Errors);
        }

        // The student applies. Only an eligible student can; the unmet requirements come back as the errors, each with its own code.
        // One Submitted or Approved application per student: checked here, and by a unique index for two requests at once.
        public async Task<Result<GraduationApplicationSummary>> ApplyAsync(Guid studentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var authorized = await _access.AuthorizeStudentAsync(Permissions.Graduation.Apply, studentId, cancellationToken);
            if (!authorized.TryGetValue(out var student))
            {
                return Result<GraduationApplicationSummary>.Fail(authorized.Errors);
            }

            if (await _applications.AnyAsync(new ActiveGraduationOfStudentSpecification(studentId), cancellationToken))
            {
                return GraduationErrors.AlreadyApplied;
            }

            var check = await _eligibility.CheckAsync(studentId, cancellationToken);
            if (!check.TryGetValue(out var eligibility))
            {
                return Result<GraduationApplicationSummary>.Fail(check.Errors);
            }

            if (!eligibility.IsEligible || student.CurrentProgram is not { } current)
            {
                return Result<GraduationApplicationSummary>.Fail(eligibility.Unmet.Count > 0 ? eligibility.Unmet : [GraduationErrors.NotEligible]);
            }

            var submitted = GraduationApplication.Submit(studentId, current.AcademicProgramId, current.StudyPlanVersionId, _clock.UtcNow);
            if (!submitted.TryGetValue(out var application))
            {
                return Result<GraduationApplicationSummary>.Fail(submitted.Errors);
            }

            _applications.Add(application);

            var entry = new AuditEntry(GraduationAuditActions.Applied, _user.UserId, "GraduationApplication", application.Id.ToString(), request.IpAddress, request.UserAgent,
                null, $"StudentId={studentId};Status={application.Status}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<GraduationApplicationSummary>.Fail(committed.Errors) : GraduationMapper.ToSummary(application);
        }

        // The student withdraws their own application while it is still Submitted.
        public async Task<Result<GraduationApplicationSummary>> CancelAsync(Guid applicationId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Graduation.Apply, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<GraduationApplicationSummary>.Fail(permission.Errors);
            }

            var application = await _applications.FirstOrDefaultAsync(new GraduationApplicationByIdSpecification(applicationId, tracking: true), cancellationToken);
            if (application is null)
            {
                return GraduationErrors.ApplicationNotFound;
            }

            var scope = await _access.AuthorizeStudentAsync(Permissions.Graduation.Apply, application.StudentId, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<GraduationApplicationSummary>.Fail(scope.Errors);
            }

            var cancelled = application.Cancel(_clock.UtcNow);
            if (cancelled.IsFailure)
            {
                return Result<GraduationApplicationSummary>.Fail(cancelled.Errors);
            }

            _applications.Update(application);

            var entry = new AuditEntry(GraduationAuditActions.Cancelled, _user.UserId, "GraduationApplication", application.Id.ToString(), request.IpAddress, request.UserAgent,
                $"Status={GraduationApplicationStatus.Submitted}", $"Status={application.Status}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<GraduationApplicationSummary>.Fail(committed.Errors) : GraduationMapper.ToSummary(application);
        }

        public async Task<Result<GraduationApplicationSummary>> GetAsync(Guid applicationId, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Graduation.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<GraduationApplicationSummary>.Fail(permission.Errors);
            }

            var application = await _applications.FirstOrDefaultAsync(new GraduationApplicationByIdSpecification(applicationId), cancellationToken);
            if (application is null)
            {
                return GraduationErrors.ApplicationNotFound;
            }

            var scope = await _access.AuthorizeStudentAsync(Permissions.Graduation.Read, application.StudentId, cancellationToken);

            return scope.IsFailure ? Result<GraduationApplicationSummary>.Fail(scope.Errors) : GraduationMapper.ToSummary(application);
        }

        public async Task<Result<IReadOnlyList<GraduationApplicationSummary>>> ListOfStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Graduation.Read, studentId, cancellationToken);
            if (!student.TryGetValue(out _))
            {
                return Result<IReadOnlyList<GraduationApplicationSummary>>.Fail(student.Errors);
            }

            var items = await _applications.ListAsync(new GraduationApplicationsOfStudentSpecification(studentId), cancellationToken);

            return Result<IReadOnlyList<GraduationApplicationSummary>>.Ok([.. items.Select(GraduationMapper.ToSummary)]);
        }

        // Every student's applications: for whole-university readers (the registrar).
        public async Task<Result<PagedResult<GraduationApplicationSummary>>> ListAsync(PaginationQuery query, GraduationApplicationStatus? status, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireUniversityWideAsync(Permissions.Graduation.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<PagedResult<GraduationApplicationSummary>>.Fail(permission.Errors);
            }

            var items = await _applications.ListAsync(new GraduationApplicationsListSpecification(query, status), cancellationToken);
            var total = await _applications.CountAsync(new GraduationApplicationsCountSpecification(status), cancellationToken);

            return PagedResult<GraduationApplicationSummary>.Create([.. items.Select(GraduationMapper.ToSummary)], query, total);
        }
    }
}
