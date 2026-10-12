using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students
{
    public sealed class StudentManagementHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IRepository<Student> students, StudentAdmissionService admission, IAcademicPlacementVerifier verifier, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private const int MaxSearchLength = 30;

        // Until the program -> department -> faculty hierarchy exists (Developer 1), staff access to
        // students needs a Global scope. Scoped roles are denied rather than allowed too much.
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<Student> _students = students;
        private readonly StudentAdmissionService _admission = admission;
        private readonly IAcademicPlacementVerifier _verifier = verifier;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, Student student, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(
                new AuditEntry(action, _user.UserId, "Student", student.Id.ToString(),
                    request.IpAddress, request.UserAgent, oldValues, newValues),
                cancellationToken);

        // ---------- create ----------

        public async Task<Result<AdmissionOutcome>> CreateAsync(AdmitStudentCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Students.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AdmissionOutcome>.Fail(auth.Errors);
            }

            return await _admission.AdmitAsync(command, request, cancellationToken);
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<StudentSummary>>> ListAsync(PaginationQuery query, string? search, StudentAcademicStatus? status, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Students.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<StudentSummary>>.Fail(auth.Errors);
            }

            var term = search?.Trim();
            if (term is { Length: > MaxSearchLength })
            {
                term = term[..MaxSearchLength];
            }

            var specification = new StudentsListSpecification(
                query, status, string.IsNullOrEmpty(term) ? null : term.ToUpperInvariant());

            var items = await _students.ListAsync(specification, cancellationToken);
            var total = await _students.CountAsync(specification, cancellationToken);

            var summaries = items.Select(StudentMapper.ToSummary).ToList();

            return Result<PagedResult<StudentSummary>>.Ok(PagedResult<StudentSummary>.Create(summaries, query, total));
        }

        public async Task<Result<StudentDetail>> GetAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Students.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentDetail>.Fail(auth.Errors);
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);

            return student is null ? StudentErrors.NotFound : StudentMapper.ToDetail(student);
        }

        // A student reading their own record: no permission needed, ownership is the check.
        public async Task<Result<StudentDetail>> GetMineAsync(CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByUserSpecification(userId), cancellationToken);

            return student is null ? StudentErrors.NotFound : StudentMapper.ToDetail(student);
        }

        // ---------- changes ----------

        public async Task<Result<StudentDetail>> ChangeStatusAsync(Guid studentId, StudentAcademicStatus? newStatus, DateOnly? effectiveDate, string? reason, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Students.ChangeStatus, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentDetail>.Fail(auth.Errors);
            }

            if (_user.UserId is not { } actor)
            {
                return Error.Unauthorized();
            }

            if (newStatus is null || effectiveDate is null)
            {
                return StudentErrors.DataInvalid;
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId, tracking: true), cancellationToken);
            if (student is null)
            {
                return StudentErrors.NotFound;
            }

            var oldStatus = student.AcademicStatus;

            var changed = student.ChangeStatus(
                newStatus.Value, effectiveDate.Value, reason, actor, _clock.UtcToday, _clock.UtcNow);

            if (changed.IsFailure)
            {
                return Result<StudentDetail>.Fail(changed.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<StudentDetail>.Fail(saved.Errors);
            }

            await AuditAsync(
                AuditActions.StudentStatusChanged, student, request,
                oldStatus.ToString(), $"{student.AcademicStatus} (effective {effectiveDate.Value:yyyy-MM-dd})",
                cancellationToken);

            return StudentMapper.ToDetail(student);
        }

        public async Task<Result<StudentDetail>> ChangeProgramAsync(Guid studentId, Guid? academicProgramId, Guid? studyPlanVersionId, Guid? academicLevelId, DateOnly? startDate, string? reason, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Students.ChangeProgram, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentDetail>.Fail(auth.Errors);
            }

            if (academicProgramId is null || studyPlanVersionId is null || startDate is null)
            {
                return StudentErrors.DataInvalid;
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId, tracking: true), cancellationToken);
            if (student is null)
            {
                return StudentErrors.NotFound;
            }

            var verified = await _verifier.VerifyProgramChangeAsync(
                academicProgramId.Value, studyPlanVersionId.Value, academicLevelId, cancellationToken);

            if (verified.IsFailure)
            {
                return Result<StudentDetail>.Fail(verified.Errors);
            }

            var oldProgram = student.CurrentProgram?.AcademicProgramId.ToString();

            var changed = student.ChangeProgram(
                academicProgramId.Value, studyPlanVersionId.Value, academicLevelId, startDate.Value,
                reason, _clock.UtcToday, _clock.UtcNow);

            if (changed.IsFailure)
            {
                return Result<StudentDetail>.Fail(changed.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<StudentDetail>.Fail(saved.Errors);
            }

            await AuditAsync(
                AuditActions.StudentProgramChanged, student, request,
                oldProgram, $"{academicProgramId.Value} (from {startDate.Value:yyyy-MM-dd})", cancellationToken);

            return StudentMapper.ToDetail(student);
        }

        public async Task<Result<StudentDetail>> UpdateAcademicProfileAsync(Guid studentId, Guid? academicLevelId, AcademicStanding? standing, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Students.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentDetail>.Fail(auth.Errors);
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId, tracking: true), cancellationToken);
            if (student is null)
            {
                return StudentErrors.NotFound;
            }

            var before = $"level={student.Profile?.CurrentAcademicLevelId}; standing={student.Profile?.AcademicStanding}";

            var updated = student.UpdateAcademicProfile(academicLevelId, standing, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<StudentDetail>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<StudentDetail>.Fail(saved.Errors);
            }

            await AuditAsync(
                AuditActions.StudentAcademicProfileUpdated, student, request, before,
                $"level={student.Profile?.CurrentAcademicLevelId}; standing={student.Profile?.AcademicStanding}",
                cancellationToken);

            return StudentMapper.ToDetail(student);
        }

        // Retry of the account creation for a student who has no account yet.
        public async Task<Result<StudentDetail>> ProvisionAccountAsync(Guid studentId, string? email, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Students.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentDetail>.Fail(auth.Errors);
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId, tracking: true), cancellationToken);
            if (student is null)
            {
                return StudentErrors.NotFound;
            }

            var provisioned = await _admission.ProvisionAccountAsync(student, email ?? string.Empty, request, cancellationToken);
            if (provisioned.IsFailure)
            {
                return Result<StudentDetail>.Fail(provisioned.Errors);
            }

            return StudentMapper.ToDetail(student);
        }
    }


}
