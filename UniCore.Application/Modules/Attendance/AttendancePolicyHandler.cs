using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Attendance.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Attendance
{
    // Attendance policies of programs and courses. Reading needs Attendance.Read; changing needs Attendance.Manage in
    // the department of the program or the course. One active policy per program and per course.
    public sealed class AttendancePolicyHandler(
        AttendanceAccess access, ICurrentUser user, IAggregateRepository<AttendancePolicy> policies, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly AttendanceAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<AttendancePolicy> _policies = policies;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(AttendancePolicy p)
            => $"Program={p.AcademicProgramId};Course={p.CourseId};Min={p.MinimumPercentage};Warn={p.WarningPercentage};Late={p.LateThresholdMinutes};Blocking={p.IsExamBlocking};Active={p.IsActive}";

        private async Task<Result<PolicySummary>> CommitAsync(AttendancePolicy policy, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "AttendancePolicy", policy.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(policy));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<PolicySummary>.Fail(committed.Errors) : AttendanceMapper.ToSummary(policy);
        }

        public async Task<Result<PagedResult<PolicySummary>>> ListAsync(PaginationQuery query, bool? activeOnly, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireAsync(Permissions.Attendance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<PolicySummary>>.Fail(auth.Errors);
            }

            var items = await _policies.ListAsync(new PoliciesListSpecification(query, activeOnly), cancellationToken);
            var total = await _policies.CountAsync(new PoliciesCountSpecification(activeOnly), cancellationToken);

            return PagedResult<PolicySummary>.Create([.. items.Select(AttendanceMapper.ToSummary)], query, total);
        }

        public async Task<Result<PolicySummary>> GetAsync(Guid policyId, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireAsync(Permissions.Attendance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PolicySummary>.Fail(auth.Errors);
            }

            var policy = await _policies.FirstOrDefaultAsync(new PolicyByIdSpecification(policyId), cancellationToken);

            return policy is null ? AttendanceErrors.PolicyNotFound : AttendanceMapper.ToSummary(policy);
        }

        public async Task<Result<PolicySummary>> CreateAsync(CreatePolicyCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Attendance.Manage, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<PolicySummary>.Fail(permission.Errors);
            }

            var created = AttendancePolicy.Create(command.AcademicProgramId, command.CourseId, command.MinimumPercentage, command.WarningPercentage, command.LateThresholdMinutes, command.IsExamBlocking, _clock.UtcNow);
            if (!created.TryGetValue(out var policy))
            {
                return Result<PolicySummary>.Fail(created.Errors);
            }

            var department = await _access.DepartmentOfPolicyTargetAsync(policy.AcademicProgramId, policy.CourseId, cancellationToken);
            if (!department.TryGetValue(out var departmentId))
            {
                return Result<PolicySummary>.Fail(department.Errors);
            }

            var scope = await _access.RequireManageInAsync(departmentId, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<PolicySummary>.Fail(scope.Errors);
            }

            if (await _policies.AnyAsync(new ActivePolicyForTargetSpecification(policy.AcademicProgramId, policy.CourseId), cancellationToken))
            {
                return AttendanceErrors.PolicyExists;
            }

            _policies.Add(policy);

            return await CommitAsync(policy, AttendanceAuditActions.PolicyCreated, null, request, cancellationToken);
        }

        public async Task<Result<PolicySummary>> UpdateAsync(Guid policyId, UpdatePolicyCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(policyId, cancellationToken);
            if (!loaded.TryGetValue(out var policy))
            {
                return Result<PolicySummary>.Fail(loaded.Errors);
            }

            var before = Describe(policy);
            var updated = policy.Update(command.MinimumPercentage, command.WarningPercentage, command.LateThresholdMinutes, command.IsExamBlocking, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<PolicySummary>.Fail(updated.Errors);
            }

            _policies.Update(policy);

            return await CommitAsync(policy, AttendanceAuditActions.PolicyUpdated, before, request, cancellationToken);
        }

        public async Task<Result<PolicySummary>> DeactivateAsync(Guid policyId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(policyId, cancellationToken);
            if (!loaded.TryGetValue(out var policy))
            {
                return Result<PolicySummary>.Fail(loaded.Errors);
            }

            var before = Describe(policy);
            policy.Deactivate(_clock.UtcNow);
            _policies.Update(policy);

            return await CommitAsync(policy, AttendanceAuditActions.PolicyDeactivated, before, request, cancellationToken);
        }

        // Manage permission -> load tracked -> scope (the department of its program or course).
        private async Task<Result<AttendancePolicy>> LoadForChangeAsync(Guid policyId, CancellationToken cancellationToken)
        {
            var permission = await _access.RequireAsync(Permissions.Attendance.Manage, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AttendancePolicy>.Fail(permission.Errors);
            }

            var policy = await _policies.FirstOrDefaultAsync(new PolicyByIdSpecification(policyId, tracking: true), cancellationToken);
            if (policy is null)
            {
                return AttendanceErrors.PolicyNotFound;
            }

            var department = await _access.DepartmentOfPolicyTargetAsync(policy.AcademicProgramId, policy.CourseId, cancellationToken);
            if (!department.TryGetValue(out var departmentId))
            {
                return Result<AttendancePolicy>.Fail(department.Errors);
            }

            var scope = await _access.RequireManageInAsync(departmentId, cancellationToken);

            return scope.IsFailure ? Result<AttendancePolicy>.Fail(scope.Errors) : policy;
        }
    }
}
