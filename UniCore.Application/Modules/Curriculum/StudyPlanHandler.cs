using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Curriculum.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Curriculum
{
    // Study plans. Reading needs Courses.Read. Changing needs Curriculum.Manage inside the program's department
    // (and the faculty above it, through IScopeHierarchy).
    public sealed class StudyPlanHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IAggregateRepository<StudyPlan> plans, IAggregateRepository<StudyPlanVersion> versions, IRepository<AcademicProgram> programs,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<StudyPlan> _plans = plans;
        private readonly IAggregateRepository<StudyPlanVersion> _versions = versions;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(StudyPlan p) => $"Program={p.AcademicProgramId};Code={p.Code};Name={p.Name};IsActive={p.IsActive}";

        private async Task<Result<StudyPlanSummary>> CommitAsync(StudyPlan plan, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "StudyPlan", plan.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(plan));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<StudyPlanSummary>.Fail(committed.Errors) : CurriculumMapper.ToSummary(plan);
        }

        // Permission first, then the program (to know its department), then the scope. A caller without the
        // permission cannot learn whether a program or plan id exists.
        private async Task<Result<AcademicProgram>> AuthorizeForProgramAsync(Guid programId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Curriculum.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AcademicProgram>.Fail(permission.Errors);
            }

            var program = programId == Guid.Empty ? null : await _programs.GetByIdAsync(programId, cancellationToken);
            if (program is null)
            {
                return CurriculumErrors.ProgramNotFound;
            }

            var scope = await _authorizer.AuthorizeAsync(Permissions.Curriculum.Manage, ResourceContext.InScope(ScopeType.Department, program.DepartmentId), cancellationToken);

            return scope.IsFailure ? Result<AcademicProgram>.Fail(scope.Errors) : program;
        }

        private async Task<Result<StudyPlan>> LoadForManageAsync(Guid planId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Curriculum.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<StudyPlan>.Fail(permission.Errors);
            }

            var plan = await _plans.GetByIdAsync(planId, cancellationToken);
            if (plan is null)
            {
                return CurriculumErrors.PlanNotFound;
            }

            var program = await AuthorizeForProgramAsync(plan.AcademicProgramId, cancellationToken);

            return program.IsFailure ? Result<StudyPlan>.Fail(program.Errors) : plan;
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<StudyPlanSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, Guid? academicProgramId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<StudyPlanSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, StudyPlansListSpecification.SortFields, "code");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<StudyPlanSummary>>.Fail(parsed.Errors);
            }

            var specification = new StudyPlansListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, academicProgramId, sortOption);

            var items = await _plans.ListAsync(specification, cancellationToken);
            var total = await _plans.CountAsync(specification, cancellationToken);

            return Result<PagedResult<StudyPlanSummary>>.Ok(PagedResult<StudyPlanSummary>.Create([.. items.Select(p => CurriculumMapper.ToSummary(p))], query, total));
        }

        public async Task<Result<StudyPlanSummary>> GetAsync(Guid planId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudyPlanSummary>.Fail(auth.Errors);
            }

            var plan = await _plans.GetByIdAsync(planId, cancellationToken);

            return plan is null ? CurriculumErrors.PlanNotFound : CurriculumMapper.ToSummary(plan);
        }

        // ---------- changes ----------

        public async Task<Result<StudyPlanSummary>> CreateAsync(CreateStudyPlanCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var authorized = await AuthorizeForProgramAsync(command.AcademicProgramId, cancellationToken);
            if (!authorized.TryGetValue(out var program))
            {
                return Result<StudyPlanSummary>.Fail(authorized.Errors);
            }

            if (!program.IsActive)
            {
                return CurriculumErrors.ProgramInactive;
            }

            var created = StudyPlan.Create(program.Id, command.Code, command.Name, command.Description, _clock.UtcNow);
            if (!created.TryGetValue(out var plan))
            {
                return Result<StudyPlanSummary>.Fail(created.Errors);
            }

            if (await _plans.AnyAsync(new StudyPlanByCodeSpecification(program.Id, plan.Code), cancellationToken))
            {
                return CurriculumErrors.PlanCodeExists;
            }

            _plans.Add(plan);

            return await CommitAsync(plan, CurriculumAuditActions.StudyPlanCreated, null, request, cancellationToken);
        }

        public async Task<Result<StudyPlanSummary>> UpdateAsync(Guid planId, UpdateStudyPlanCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(planId, cancellationToken);
            if (!loaded.TryGetValue(out var plan))
            {
                return Result<StudyPlanSummary>.Fail(loaded.Errors);
            }

            var before = Describe(plan);

            var updated = plan.UpdateDetails(command.Name, command.Description, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<StudyPlanSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(plan, CurriculumAuditActions.StudyPlanUpdated, before, request, cancellationToken);
        }

        public async Task<Result<StudyPlanSummary>> ActivateAsync(Guid planId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(planId, cancellationToken);
            if (!loaded.TryGetValue(out var plan))
            {
                return Result<StudyPlanSummary>.Fail(loaded.Errors);
            }

            // A plan of an inactive program stays inactive until the program is reactivated.
            var program = await _programs.GetByIdAsync(plan.AcademicProgramId, cancellationToken);
            if (program is null || !program.IsActive)
            {
                return CurriculumErrors.ProgramInactive;
            }

            var before = Describe(plan);

            var activated = plan.Activate(_clock.UtcNow);
            if (activated.IsFailure)
            {
                return Result<StudyPlanSummary>.Fail(activated.Errors);
            }

            return await CommitAsync(plan, CurriculumAuditActions.StudyPlanActivated, before, request, cancellationToken);
        }

        public async Task<Result<StudyPlanSummary>> DeactivateAsync(Guid planId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(planId, cancellationToken);
            if (!loaded.TryGetValue(out var plan))
            {
                return Result<StudyPlanSummary>.Fail(loaded.Errors);
            }

            var hasActiveVersion = await _versions.AnyAsync(new VersionByStatusSpecification(plan.Id, StudyPlanVersionStatus.Active), cancellationToken);

            var before = Describe(plan);

            var deactivated = plan.Deactivate(hasActiveVersion, _clock.UtcNow);
            if (deactivated.IsFailure)
            {
                return Result<StudyPlanSummary>.Fail(deactivated.Errors);
            }

            return await CommitAsync(plan, CurriculumAuditActions.StudyPlanDeactivated, before, request, cancellationToken);
        }
    }
}
