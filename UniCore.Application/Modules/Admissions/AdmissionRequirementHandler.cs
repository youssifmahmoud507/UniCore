using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Admissions.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Admissions
{
    // The requirement catalog of a program. Reading needs Admissions.Read. Changing needs Admissions.Decide
    // inside the program's department (the program's scope, and the faculty above it through IScopeHierarchy).
    public sealed class AdmissionRequirementHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IAggregateRepository<AdmissionRequirement> requirements, IRepository<AcademicProgram> programs,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<AdmissionRequirement> _requirements = requirements;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task AuditAsync(string action, AdmissionRequirement r, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken)
            => _audit.WriteAsync(new AuditEntry(action, _user.UserId, "AdmissionRequirement", r.Id.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues), cancellationToken);

        private static string Describe(AdmissionRequirement r) => $"Code={r.Code};Name={r.Name};Mandatory={r.IsMandatory};IsActive={r.IsActive}";

        private async Task<Result<AcademicProgram>> LoadProgramForManageAsync(Guid programId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Admissions.Decide, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AcademicProgram>.Fail(permission.Errors);
            }

            var program = programId == Guid.Empty ? null : await _programs.GetByIdAsync(programId, cancellationToken);
            if (program is null)
            {
                return AdmissionsErrors.ProgramNotFound;
            }

            var scope = await _authorizer.AuthorizeAsync(Permissions.Admissions.Decide, ResourceContext.InScope(ScopeType.Department, program.DepartmentId), cancellationToken);
            if (scope.IsFailure)
            {
                return Result<AcademicProgram>.Fail(scope.Errors);
            }

            return program;
        }

        private async Task<Result<AdmissionRequirement>> LoadForManageAsync(Guid requirementId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Admissions.Decide, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AdmissionRequirement>.Fail(permission.Errors);
            }

            var requirement = await _requirements.FirstOrDefaultAsync(new RequirementByIdSpecification(requirementId), cancellationToken);
            if (requirement is null)
            {
                return AdmissionsErrors.RequirementNotFound;
            }

            var program = await LoadProgramForManageAsync(requirement.AcademicProgramId, cancellationToken);
            if (program.IsFailure)
            {
                return Result<AdmissionRequirement>.Fail(program.Errors);
            }

            return requirement;
        }

        public async Task<Result<IReadOnlyList<RequirementSummary>>> ListAsync(Guid programId, bool activeOnly, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Admissions.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<RequirementSummary>>.Fail(auth.Errors);
            }

            var items = await _requirements.ListAsync(new RequirementsOfProgramSpecification(programId, activeOnly), cancellationToken);

            return Result<IReadOnlyList<RequirementSummary>>.Ok([.. items.Select(x => AdmissionsMapper.ToSummary(x))]);
        }

        public async Task<Result<RequirementSummary>> CreateAsync(CreateRequirementCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadProgramForManageAsync(command.AcademicProgramId, cancellationToken);
            if (!loaded.TryGetValue(out var program))
            {
                return Result<RequirementSummary>.Fail(loaded.Errors);
            }

            if (!program.IsActive)
            {
                return AdmissionsErrors.ProgramInactive;
            }

            var created = AdmissionRequirement.Create(program.Id, command.Code, command.Name, command.Description, command.IsMandatory, _clock.UtcNow);
            if (!created.TryGetValue(out var requirement))
            {
                return Result<RequirementSummary>.Fail(created.Errors);
            }

            if (await _requirements.AnyAsync(new RequirementByCodeSpecification(program.Id, requirement.Code), cancellationToken))
            {
                return AdmissionsErrors.RequirementCodeExists;
            }

            _requirements.Add(requirement);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<RequirementSummary>.Fail(saved.Errors);
            }

            await AuditAsync(AdmissionsAuditActions.RequirementCreated, requirement, request, null, Describe(requirement), cancellationToken);

            return AdmissionsMapper.ToSummary(requirement);
        }

        public async Task<Result<RequirementSummary>> UpdateAsync(Guid requirementId, UpdateRequirementCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(requirementId, cancellationToken);
            if (!loaded.TryGetValue(out var requirement))
            {
                return Result<RequirementSummary>.Fail(loaded.Errors);
            }

            var before = Describe(requirement);

            var updated = requirement.UpdateDetails(command.Name, command.Description, command.IsMandatory, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<RequirementSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(AdmissionsAuditActions.RequirementUpdated, requirement, request, before, cancellationToken);
        }

        public async Task<Result<RequirementSummary>> ActivateAsync(Guid requirementId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(requirementId, cancellationToken);
            if (!loaded.TryGetValue(out var requirement))
            {
                return Result<RequirementSummary>.Fail(loaded.Errors);
            }

            var before = Describe(requirement);

            var changed = requirement.Activate(_clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<RequirementSummary>.Fail(changed.Errors);
            }

            return await CommitAsync(AdmissionsAuditActions.RequirementActivated, requirement, request, before, cancellationToken);
        }

        // Existing applications keep their copy of the requirement; only new applications stop getting it.
        public async Task<Result<RequirementSummary>> DeactivateAsync(Guid requirementId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(requirementId, cancellationToken);
            if (!loaded.TryGetValue(out var requirement))
            {
                return Result<RequirementSummary>.Fail(loaded.Errors);
            }

            var before = Describe(requirement);

            var changed = requirement.Deactivate(_clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<RequirementSummary>.Fail(changed.Errors);
            }

            return await CommitAsync(AdmissionsAuditActions.RequirementDeactivated, requirement, request, before, cancellationToken);
        }

        private async Task<Result<RequirementSummary>> CommitAsync(string action, AdmissionRequirement requirement, RequestInfo request, string before, CancellationToken cancellationToken)
        {
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<RequirementSummary>.Fail(saved.Errors);
            }

            await AuditAsync(action, requirement, request, before, Describe(requirement), cancellationToken);

            return AdmissionsMapper.ToSummary(requirement);
        }
    }
}
