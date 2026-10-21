using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Curriculum.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Curriculum
{
    // Versions of a study plan and their content. Reading needs Courses.Read; every change needs Curriculum.Manage
    // inside the program's department. Only a Draft can change. Order of checks: the permission alone, then load,
    // then the scope, so a caller without the permission cannot learn whether an id exists.
    public sealed class StudyPlanVersionHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IAggregateRepository<StudyPlanVersion> versions, IAggregateRepository<StudyPlan> plans, IRepository<AcademicProgram> programs,
        IRepository<AcademicLevel> levels, IRepository<AcademicTerm> terms, ICourseVersionVerifier courseVersions,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<StudyPlanVersion> _versions = versions;
        private readonly IAggregateRepository<StudyPlan> _plans = plans;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IRepository<AcademicLevel> _levels = levels;
        private readonly IRepository<AcademicTerm> _terms = terms;
        private readonly ICourseVersionVerifier _courseVersions = courseVersions;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // The loaded version with the plan and program above it (needed for scope and for level checks).
        private sealed record Loaded(StudyPlanVersion Version, StudyPlan Plan, AcademicProgram Program);

        private static string Describe(StudyPlanVersion v) => $"Plan={v.StudyPlanId};Version={v.VersionNumber};Status={v.Status};From={v.EffectiveFrom:yyyy-MM-dd};To={v.EffectiveTo:yyyy-MM-dd}";

        private AuditEntry Entry(string action, StudyPlanVersion v, RequestInfo request, string? before, string? after = null)
            => new(action, _user.UserId, "StudyPlanVersion", v.Id.ToString(), request.IpAddress, request.UserAgent, before, after ?? Describe(v));

        private async Task<Result<VersionDetail>> CommitAsync(StudyPlanVersion version, AuditEntry entry, CancellationToken cancellationToken)
        {
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<VersionDetail>.Fail(committed.Errors) : CurriculumMapper.ToDetail(version);
        }

        private async Task<Result<(StudyPlan Plan, AcademicProgram Program)>> AuthorizeForPlanAsync(Guid planId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Curriculum.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<(StudyPlan, AcademicProgram)>.Fail(permission.Errors);
            }

            var plan = await _plans.GetByIdAsync(planId, cancellationToken);
            if (plan is null)
            {
                return CurriculumErrors.PlanNotFound;
            }

            var program = await _programs.GetByIdAsync(plan.AcademicProgramId, cancellationToken);
            if (program is null)
            {
                return CurriculumErrors.ProgramNotFound;
            }

            var scope = await _authorizer.AuthorizeAsync(Permissions.Curriculum.Manage, ResourceContext.InScope(ScopeType.Department, program.DepartmentId), cancellationToken);
            if (scope.IsFailure)
            {
                return Result<(StudyPlan, AcademicProgram)>.Fail(scope.Errors);
            }

            return (plan, program);
        }

        private async Task<Result<Loaded>> LoadForManageAsync(Guid versionId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Curriculum.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Loaded>.Fail(permission.Errors);
            }

            var version = await _versions.FirstOrDefaultAsync(new VersionWithDetailsSpecification(versionId, tracking: true), cancellationToken);
            if (version is null)
            {
                return CurriculumErrors.VersionNotFound;
            }

            var context = await AuthorizeForPlanAsync(version.StudyPlanId, cancellationToken);
            if (!context.TryGetValue(out var found))
            {
                return Result<Loaded>.Fail(context.Errors);
            }

            return new Loaded(version, found.Plan, found.Program);
        }

        // The level must exist, be active and belong to the same program. The term must exist and be active.
        private async Task<Result> CheckPlacementAsync(Guid programId, Guid? levelId, Guid? termId, CancellationToken cancellationToken)
        {
            if (levelId is { } level && level != Guid.Empty)
            {
                var found = await _levels.GetByIdAsync(level, cancellationToken);
                if (found is null)
                {
                    return Result.Fail(CurriculumErrors.LevelNotFound);
                }

                if (!found.IsActive || found.AcademicProgramId != programId)
                {
                    return Result.Fail(CurriculumErrors.LevelInvalid);
                }
            }

            if (termId is { } term && term != Guid.Empty)
            {
                var found = await _terms.GetByIdAsync(term, cancellationToken);
                if (found is null)
                {
                    return Result.Fail(CurriculumErrors.TermNotFound);
                }

                if (!found.IsActive)
                {
                    return Result.Fail(CurriculumErrors.TermInactive);
                }
            }

            return Result.Ok();
        }

        // ---------- queries ----------

        public async Task<Result<IReadOnlyList<VersionSummary>>> ListAsync(Guid planId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<VersionSummary>>.Fail(auth.Errors);
            }

            if (await _plans.GetByIdAsync(planId, cancellationToken) is null)
            {
                return CurriculumErrors.PlanNotFound;
            }

            var items = await _versions.ListAsync(new VersionsOfPlanSpecification(planId), cancellationToken);

            return Result<IReadOnlyList<VersionSummary>>.Ok([.. items.Select(v => CurriculumMapper.ToSummary(v))]);
        }

        public async Task<Result<VersionDetail>> GetAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<VersionDetail>.Fail(auth.Errors);
            }

            var version = await _versions.FirstOrDefaultAsync(new VersionWithDetailsSpecification(versionId, tracking: false), cancellationToken);

            return version is null ? CurriculumErrors.VersionNotFound : CurriculumMapper.ToDetail(version);
        }

        // ---------- draft ----------

        public async Task<Result<VersionDetail>> CreateAsync(Guid planId, CreateVersionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var context = await AuthorizeForPlanAsync(planId, cancellationToken);
            if (!context.TryGetValue(out var found))
            {
                return Result<VersionDetail>.Fail(context.Errors);
            }

            if (!found.Plan.IsActive)
            {
                return CurriculumErrors.PlanInactive;
            }

            if (!found.Program.IsActive)
            {
                return CurriculumErrors.ProgramInactive;
            }

            if (command.EffectiveFrom is not { } effectiveFrom)
            {
                return CurriculumErrors.EffectiveDateInvalid;
            }

            if (await _versions.AnyAsync(new VersionByStatusSpecification(planId, StudyPlanVersionStatus.Draft), cancellationToken))
            {
                return CurriculumErrors.DraftExists;
            }

            var latest = (await _versions.ListAsync(new LatestVersionOfPlanSpecification(planId), cancellationToken)).FirstOrDefault();
            var number = (latest?.VersionNumber ?? 0) + 1;

            Result<StudyPlanVersion> created;

            if (command.SourceVersionId is { } sourceId)
            {
                var source = await _versions.FirstOrDefaultAsync(new VersionWithDetailsSpecification(sourceId, tracking: false), cancellationToken);
                if (source is null || source.StudyPlanId != planId)
                {
                    return CurriculumErrors.VersionNotFound;
                }

                created = StudyPlanVersion.CreateDraftFrom(source, number, effectiveFrom, _clock.UtcNow);
            }
            else
            {
                created = StudyPlanVersion.Create(planId, number, effectiveFrom, _clock.UtcNow);
            }

            if (!created.TryGetValue(out var version))
            {
                return Result<VersionDetail>.Fail(created.Errors);
            }

            _versions.Add(version);

            return await CommitAsync(version, Entry(CurriculumAuditActions.VersionCreated, version, request, null, Describe(version) + (command.SourceVersionId is { } s ? $";CopiedFrom={s}" : string.Empty)), cancellationToken);
        }

        public async Task<Result<VersionDetail>> UpdateAsync(Guid versionId, UpdateVersionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<VersionDetail>.Fail(loaded.Errors);
            }

            if (command.EffectiveFrom is not { } effectiveFrom)
            {
                return CurriculumErrors.EffectiveDateInvalid;
            }

            var before = Describe(context.Version);

            var changed = context.Version.ChangeEffectiveFrom(effectiveFrom, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<VersionDetail>.Fail(changed.Errors);
            }

            return await CommitAsync(context.Version, Entry(CurriculumAuditActions.VersionUpdated, context.Version, request, before), cancellationToken);
        }

        // ---------- plan courses ----------

        public async Task<Result<VersionDetail>> AddCourseAsync(Guid versionId, AddPlanCourseCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<VersionDetail>.Fail(loaded.Errors);
            }

            if (command.RequirementType is not { } requirementType)
            {
                return CurriculumErrors.RequirementTypeInvalid;
            }

            if (command.CourseVersionId == Guid.Empty)
            {
                return CurriculumErrors.CourseVersionInvalid;
            }

            // A version that is not a draft is refused by the domain; check it first so a wrong state wins over a lookup.
            if (context.Version.Status != StudyPlanVersionStatus.Draft)
            {
                return CurriculumErrors.VersionNotDraft;
            }

            var verified = await _courseVersions.VerifyAsync(command.CourseVersionId, cancellationToken);
            if (verified.IsFailure)
            {
                return Result<VersionDetail>.Fail(verified.Errors);
            }

            var placement = await CheckPlacementAsync(context.Program.Id, command.AcademicLevelId, command.RecommendedTermId, cancellationToken);
            if (placement.IsFailure)
            {
                return Result<VersionDetail>.Fail(placement.Errors);
            }

            var before = Describe(context.Version);

            var added = context.Version.AddCourse(command.CourseVersionId, command.AcademicLevelId, command.RecommendedTermId, requirementType, command.RequiredCredits, _clock.UtcNow);
            if (added.IsFailure)
            {
                return Result<VersionDetail>.Fail(added.Errors);
            }

            return await CommitAsync(context.Version, Entry(CurriculumAuditActions.CourseAdded, context.Version, request, before, $"{Describe(context.Version)};CourseVersion={command.CourseVersionId};Type={requirementType}"), cancellationToken);
        }

        public async Task<Result<VersionDetail>> UpdateCourseAsync(Guid versionId, Guid studyPlanCourseId, UpdatePlanCourseCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<VersionDetail>.Fail(loaded.Errors);
            }

            if (command.RequirementType is not { } requirementType)
            {
                return CurriculumErrors.RequirementTypeInvalid;
            }

            if (context.Version.Status != StudyPlanVersionStatus.Draft)
            {
                return CurriculumErrors.VersionNotDraft;
            }

            var placement = await CheckPlacementAsync(context.Program.Id, command.AcademicLevelId, command.RecommendedTermId, cancellationToken);
            if (placement.IsFailure)
            {
                return Result<VersionDetail>.Fail(placement.Errors);
            }

            var before = Describe(context.Version);

            var updated = context.Version.UpdateCourse(studyPlanCourseId, command.AcademicLevelId, command.RecommendedTermId, requirementType, command.RequiredCredits, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<VersionDetail>.Fail(updated.Errors);
            }

            return await CommitAsync(context.Version, Entry(CurriculumAuditActions.CourseUpdated, context.Version, request, before, $"{Describe(context.Version)};PlanCourse={studyPlanCourseId};Type={requirementType}"), cancellationToken);
        }

        public async Task<Result<VersionDetail>> RemoveCourseAsync(Guid versionId, Guid studyPlanCourseId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<VersionDetail>.Fail(loaded.Errors);
            }

            var before = Describe(context.Version);

            var removed = context.Version.RemoveCourse(studyPlanCourseId, _clock.UtcNow);
            if (!removed.TryGetValue(out var course))
            {
                return Result<VersionDetail>.Fail(removed.Errors);
            }

            return await CommitAsync(context.Version, Entry(CurriculumAuditActions.CourseRemoved, context.Version, request, before, $"{Describe(context.Version)};CourseVersion={course.CourseVersionId}"), cancellationToken);
        }

        // ---------- graduation requirements ----------

        public async Task<Result<VersionDetail>> SetRequirementAsync(Guid versionId, SetRequirementCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<VersionDetail>.Fail(loaded.Errors);
            }

            if (command.RequirementType is not { } requirementType)
            {
                return CurriculumErrors.RequirementTypeInvalid;
            }

            var before = Describe(context.Version);

            var set = context.Version.SetRequirement(command.CourseVersionId, requirementType, _clock.UtcNow);
            if (set.IsFailure)
            {
                return Result<VersionDetail>.Fail(set.Errors);
            }

            return await CommitAsync(context.Version, Entry(CurriculumAuditActions.RequirementSet, context.Version, request, before, $"{Describe(context.Version)};CourseVersion={command.CourseVersionId};Type={requirementType}"), cancellationToken);
        }

        public async Task<Result<VersionDetail>> RemoveRequirementAsync(Guid versionId, Guid courseVersionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<VersionDetail>.Fail(loaded.Errors);
            }

            var before = Describe(context.Version);

            var removed = context.Version.RemoveRequirement(courseVersionId, _clock.UtcNow);
            if (removed.IsFailure)
            {
                return Result<VersionDetail>.Fail(removed.Errors);
            }

            return await CommitAsync(context.Version, Entry(CurriculumAuditActions.RequirementRemoved, context.Version, request, before, $"{Describe(context.Version)};CourseVersion={courseVersionId}"), cancellationToken);
        }

        // ---------- lifecycle ----------

        // Draft -> Active. The version it replaces (the plan's current Active one) is retired on the day before,
        // in the same save. The new version must start after the one it replaces.
        public async Task<Result<VersionDetail>> ActivateAsync(Guid versionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<VersionDetail>.Fail(loaded.Errors);
            }

            var version = context.Version;

            if (version.Status != StudyPlanVersionStatus.Draft)
            {
                return CurriculumErrors.VersionInvalidState;
            }

            if (!context.Plan.IsActive)
            {
                return CurriculumErrors.PlanInactive;
            }

            var previous = await _versions.FirstOrDefaultAsync(new VersionByStatusSpecification(version.StudyPlanId, StudyPlanVersionStatus.Active, tracking: true), cancellationToken);
            if (previous is not null && version.EffectiveFrom <= previous.EffectiveFrom)
            {
                return CurriculumErrors.EffectiveDateInvalid;
            }

            var before = Describe(version);
            var now = _clock.UtcNow;

            var activated = version.Activate(now);
            if (activated.IsFailure)
            {
                return Result<VersionDetail>.Fail(activated.Errors);
            }

            string? retiredBefore = null;

            if (previous is not null)
            {
                retiredBefore = Describe(previous);

                var retired = previous.Retire(version.EffectiveFrom.AddDays(-1), now);
                if (retired.IsFailure)
                {
                    return Result<VersionDetail>.Fail(retired.Errors);
                }
            }

            var committed = await CommitAsync(version, Entry(CurriculumAuditActions.VersionActivated, version, request, before), cancellationToken);

            if (committed.IsSuccess && previous is not null)
            {
                await _audit.WriteAsync(Entry(CurriculumAuditActions.VersionRetired, previous, request, retiredBefore), cancellationToken);
            }

            return committed;
        }

        // Active -> Retired with the last effective day. Students already on the version keep it.
        public async Task<Result<VersionDetail>> RetireAsync(Guid versionId, RetireVersionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<VersionDetail>.Fail(loaded.Errors);
            }

            if (command.EffectiveTo is not { } effectiveTo)
            {
                return CurriculumErrors.EffectiveToInvalid;
            }

            var before = Describe(context.Version);

            var retired = context.Version.Retire(effectiveTo, _clock.UtcNow);
            if (retired.IsFailure)
            {
                return Result<VersionDetail>.Fail(retired.Errors);
            }

            return await CommitAsync(context.Version, Entry(CurriculumAuditActions.VersionRetired, context.Version, request, before), cancellationToken);
        }
    }
}
