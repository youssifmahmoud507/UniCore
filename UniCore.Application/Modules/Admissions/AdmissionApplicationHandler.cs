using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Admissions.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Admissions
{
    // The application workflow. Staff act on the applicant's behalf until the applicant portal exists
    // (a self-service "Admissions.Apply" permission is proposed in developer1-integration-notes.md).
    //   Admissions.Read    list / get
    //   Admissions.Review  create, attach documents, submit, withdraw, start review, review requirements
    //   Admissions.Decide  accept, conditionally accept, complete conditions, reject
    // Every check follows the same order: the permission alone first (so a caller without it cannot learn whether
    // an id exists), then load, then the scope of the program's department.
    public sealed class AdmissionApplicationHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IAggregateRepository<AdmissionApplication> applications, IRepository<Applicant> applicants,
        IAggregateRepository<AdmissionRequirement> requirements, IRepository<AcademicProgram> programs,
        IAdmissionDeadlineProvider deadlines, IAdmissionDocumentVerifier documents,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<AdmissionApplication> _applications = applications;
        private readonly IRepository<Applicant> _applicants = applicants;
        private readonly IAggregateRepository<AdmissionRequirement> _requirements = requirements;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IAdmissionDeadlineProvider _deadlines = deadlines;
        private readonly IAdmissionDocumentVerifier _documents = documents;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // ---------- shared helpers ----------

        private Task AuditAsync(string action, AdmissionApplication application, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken)
            => _audit.WriteAsync(new AuditEntry(action, _user.UserId, "AdmissionApplication", application.Id.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues), cancellationToken);

        private static string Describe(AdmissionApplication a) => $"Status={a.Status};Program={a.AcademicProgramId};AcademicYear={a.AcademicYearId}";

        private Task<Result> RequireInProgramScopeAsync(string permission, Guid departmentId, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(permission, ResourceContext.InScope(ScopeType.Department, departmentId), cancellationToken);

        // Permission -> load (with requirements and decisions) -> scope of the program's department.
        private async Task<Result<AdmissionApplication>> LoadAsync(Guid applicationId, string permission, bool tracking, CancellationToken cancellationToken)
        {
            var allowed = await _authorizer.AuthorizeAsync(permission, null, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<AdmissionApplication>.Fail(allowed.Errors);
            }

            var application = await _applications.FirstOrDefaultAsync(new ApplicationWithDetailsSpecification(applicationId, tracking), cancellationToken);
            if (application is null)
            {
                return AdmissionsErrors.ApplicationNotFound;
            }

            var program = await _programs.GetByIdAsync(application.AcademicProgramId, cancellationToken);
            if (program is null)
            {
                return AdmissionsErrors.ProgramNotFound;
            }

            var scope = await RequireInProgramScopeAsync(permission, program.DepartmentId, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<AdmissionApplication>.Fail(scope.Errors);
            }

            return application;
        }

        // Runs one state change, saves, then audits (only after a successful save).
        private async Task<Result<ApplicationDetail>> ChangeAsync(Guid applicationId, string permission, Func<AdmissionApplication, Result> change, string auditAction, RequestInfo request, string? extraAudit, CancellationToken cancellationToken)
        {
            var loaded = await LoadAsync(applicationId, permission, tracking: true, cancellationToken);
            if (!loaded.TryGetValue(out var application))
            {
                return Result<ApplicationDetail>.Fail(loaded.Errors);
            }

            var before = Describe(application);

            var changed = change(application);
            if (changed.IsFailure)
            {
                return Result<ApplicationDetail>.Fail(changed.Errors);
            }

            return await SaveAsync(application, auditAction, request, before, extraAudit, cancellationToken);
        }

        private async Task<Result<ApplicationDetail>> SaveAsync(AdmissionApplication application, string auditAction, RequestInfo request, string before, string? extraAudit, CancellationToken cancellationToken)
        {
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ApplicationDetail>.Fail(saved.Errors);
            }

            var after = extraAudit is null ? Describe(application) : $"{Describe(application)};{extraAudit}";
            await AuditAsync(auditAction, application, request, before, after, cancellationToken);

            return AdmissionsMapper.ToDetail(application);
        }

        private Guid CurrentUserId => _user.UserId ?? Guid.Empty;

        // ---------- queries ----------

        public async Task<Result<PagedResult<ApplicationSummary>>> ListAsync(PaginationQuery query, string? sort, AdmissionApplicationStatus? status, Guid? programId, Guid? academicYearId, Guid? applicantId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Admissions.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<ApplicationSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, ApplicationsListSpecification.SortFields, "createdAt");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<ApplicationSummary>>.Fail(parsed.Errors);
            }

            var specification = new ApplicationsListSpecification(query, status, programId, academicYearId, applicantId, sortOption);

            var items = await _applications.ListAsync(specification, cancellationToken);
            var total = await _applications.CountAsync(specification, cancellationToken);

            return Result<PagedResult<ApplicationSummary>>.Ok(PagedResult<ApplicationSummary>.Create([.. items.Select(x => AdmissionsMapper.ToSummary(x))], query, total));
        }

        public async Task<Result<ApplicationDetail>> GetAsync(Guid applicationId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(applicationId, Permissions.Admissions.Read, tracking: false, cancellationToken);

            return loaded.TryGetValue(out var application) ? AdmissionsMapper.ToDetail(application) : Result<ApplicationDetail>.Fail(loaded.Errors);
        }

        // ---------- changes ----------

        public async Task<Result<ApplicationDetail>> CreateAsync(CreateApplicationCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var allowed = await _authorizer.AuthorizeAsync(Permissions.Admissions.Review, null, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<ApplicationDetail>.Fail(allowed.Errors);
            }

            if (command.ApplicantId == Guid.Empty || command.AcademicProgramId == Guid.Empty || command.AcademicYearId == Guid.Empty)
            {
                return AdmissionsErrors.InvalidReference;
            }

            var program = await _programs.GetByIdAsync(command.AcademicProgramId, cancellationToken);
            if (program is null)
            {
                return AdmissionsErrors.ProgramNotFound;
            }

            var scope = await RequireInProgramScopeAsync(Permissions.Admissions.Review, program.DepartmentId, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<ApplicationDetail>.Fail(scope.Errors);
            }

            if (!program.IsActive)
            {
                return AdmissionsErrors.ProgramInactive;
            }

            if (await _applicants.GetByIdAsync(command.ApplicantId, cancellationToken) is null)
            {
                return AdmissionsErrors.ApplicantNotFound;
            }

            var deadline = await _deadlines.GetDeadlineAsync(command.AcademicYearId, cancellationToken);
            if (!deadline.TryGetValue(out var deadlineValue))
            {
                return Result<ApplicationDetail>.Fail(deadline.Errors);
            }

            if (await _applications.AnyAsync(new ActiveApplicationSpecification(command.ApplicantId, command.AcademicProgramId, command.AcademicYearId), cancellationToken))
            {
                return AdmissionsErrors.DuplicateApplication;
            }

            var catalog = await _requirements.ListAsync(new RequirementsOfProgramSpecification(program.Id, activeOnly: true), cancellationToken);

            var created = AdmissionApplication.Create(command.ApplicantId, program.Id, command.AcademicYearId, deadlineValue, [.. catalog], _clock.UtcNow);
            if (!created.TryGetValue(out var application))
            {
                return Result<ApplicationDetail>.Fail(created.Errors);
            }

            _applications.Add(application);

            return await SaveAsync(application, AdmissionsAuditActions.ApplicationCreated, request, string.Empty, null, cancellationToken);
        }

        public Task<Result<ApplicationDetail>> SubmitAsync(Guid applicationId, RequestInfo request, CancellationToken cancellationToken = default)
            => ChangeAsync(applicationId, Permissions.Admissions.Review, a => a.Submit(_clock.UtcNow), AdmissionsAuditActions.ApplicationSubmitted, request, null, cancellationToken);

        public Task<Result<ApplicationDetail>> WithdrawAsync(Guid applicationId, RequestInfo request, CancellationToken cancellationToken = default)
            => ChangeAsync(applicationId, Permissions.Admissions.Review, a => a.Withdraw(_clock.UtcNow), AdmissionsAuditActions.ApplicationWithdrawn, request, null, cancellationToken);

        public Task<Result<ApplicationDetail>> StartReviewAsync(Guid applicationId, RequestInfo request, CancellationToken cancellationToken = default)
            => ChangeAsync(applicationId, Permissions.Admissions.Review, a => a.StartReview(_clock.UtcNow), AdmissionsAuditActions.ReviewStarted, request, null, cancellationToken);

        public async Task<Result<ApplicationDetail>> AttachDocumentAsync(Guid applicationId, Guid applicationRequirementId, AttachDocumentCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            // Permission first, so a caller without it cannot probe which documents exist. The scope check follows
            // inside ChangeAsync, once the application is loaded.
            var allowed = await _authorizer.AuthorizeAsync(Permissions.Admissions.Review, null, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<ApplicationDetail>.Fail(allowed.Errors);
            }

            if (command.DocumentId == Guid.Empty)
            {
                return AdmissionsErrors.DocumentInvalid;
            }

            var verified = await _documents.VerifyAsync(command.DocumentId, cancellationToken);
            if (verified.IsFailure)
            {
                return Result<ApplicationDetail>.Fail(verified.Errors);
            }

            return await ChangeAsync(applicationId, Permissions.Admissions.Review,
                a => a.AttachDocument(applicationRequirementId, command.DocumentId, _clock.UtcNow),
                AdmissionsAuditActions.DocumentAttached, request, $"Requirement={applicationRequirementId};Document={command.DocumentId}", cancellationToken);
        }

        public Task<Result<ApplicationDetail>> ReviewRequirementAsync(Guid applicationId, Guid applicationRequirementId, ReviewRequirementCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (command.Approve is not { } approve)
            {
                // The permission is still checked first, so a caller without it gets Forbidden, not a validation hint.
                return RejectAfterPermissionAsync<ApplicationDetail>(Permissions.Admissions.Review, AdmissionsErrors.ReviewDecisionRequired, cancellationToken);
            }

            return ChangeAsync(applicationId, Permissions.Admissions.Review,
                a => a.ReviewRequirement(applicationRequirementId, approve, command.Note, CurrentUserId, _clock.UtcNow),
                approve ? AdmissionsAuditActions.RequirementApproved : AdmissionsAuditActions.RequirementRejected,
                request, $"Requirement={applicationRequirementId}", cancellationToken);
        }

        public Task<Result<ApplicationDetail>> AcceptAsync(Guid applicationId, DecisionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
            => ChangeAsync(applicationId, Permissions.Admissions.Decide, a => a.Accept(command.Reason, CurrentUserId, _clock.UtcNow), AdmissionsAuditActions.ApplicationAccepted, request, null, cancellationToken);

        public Task<Result<ApplicationDetail>> ConditionallyAcceptAsync(Guid applicationId, DecisionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
            => ChangeAsync(applicationId, Permissions.Admissions.Decide, a => a.ConditionallyAccept(command.Reason, CurrentUserId, _clock.UtcNow), AdmissionsAuditActions.ApplicationConditionallyAccepted, request, null, cancellationToken);

        public Task<Result<ApplicationDetail>> CompleteConditionsAsync(Guid applicationId, DecisionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
            => ChangeAsync(applicationId, Permissions.Admissions.Decide, a => a.CompleteConditions(command.Reason, CurrentUserId, _clock.UtcNow), AdmissionsAuditActions.ApplicationAccepted, request, "Via=ConditionsCompleted", cancellationToken);

        public Task<Result<ApplicationDetail>> RejectAsync(Guid applicationId, DecisionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
            => ChangeAsync(applicationId, Permissions.Admissions.Decide, a => a.Reject(command.Reason, CurrentUserId, _clock.UtcNow), AdmissionsAuditActions.ApplicationRejected, request, null, cancellationToken);

        private async Task<Result<T>> RejectAfterPermissionAsync<T>(string permission, Error error, CancellationToken cancellationToken)
        {
            var allowed = await _authorizer.AuthorizeAsync(permission, null, cancellationToken);

            return allowed.IsFailure ? Result<T>.Fail(allowed.Errors) : Result<T>.Fail(error);
        }
    }
}
